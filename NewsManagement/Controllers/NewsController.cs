﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using NewsManagement.Models;

namespace NewsManagement.Controllers
{
    public class NewsController : Controller
    {
        private TinTucEntities2 db = new TinTucEntities2();

        public ActionResult Index(int? categoryId, string search, int? lastId = null, int pageSize = 10)
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                List<NewsListItem> newsList;
                bool hasNextPage = false;
                int? nextLastId = null;

                if (!string.IsNullOrEmpty(search))
                {
                    // 🚀 Tận dụng IX_News_Title_Status có sẵn
                    var searchSql = @"
                SELECT TOP (@pageSize) 
                    n.Id, n.Title, n.Summary, n.CreatedDate, n.Status, n.Ordering,
                    STUFF((SELECT ', ' + c.Name 
                           FROM NewsCategory nc2 WITH (NOLOCK)
                           INNER JOIN Category c WITH (NOLOCK) ON nc2.CategoryId = c.Id 
                           WHERE nc2.NewsId = n.Id 
                           FOR XML PATH('')), 1, 2, '') as CategoryNames
                FROM News n WITH (NOLOCK)
                WHERE n.Status = 1 
                AND n.Title LIKE @search  -- Tận dụng IX_News_Title_Status
                AND (@lastId IS NULL OR n.Id < @lastId)
                ORDER BY n.Id DESC";

                    // Fallback cho Summary search
                    var searchSummarySQL = @"
                SELECT TOP (@pageSize) 
                    n.Id, n.Title, n.Summary, n.CreatedDate, n.Status, n.Ordering,
                    STUFF((SELECT ', ' + c.Name 
                           FROM NewsCategory nc2 WITH (NOLOCK)
                           INNER JOIN Category c WITH (NOLOCK) ON nc2.CategoryId = c.Id 
                           WHERE nc2.NewsId = n.Id 
                           FOR XML PATH('')), 1, 2, '') as CategoryNames
                FROM News n WITH (NOLOCK)
                WHERE n.Status = 1 
                AND (n.Title LIKE @search OR n.Summary LIKE @search)
                AND (@lastId IS NULL OR n.Id < @lastId)
                ORDER BY n.Id DESC";

                    var searchTerm = "%" + search + "%";

                    // Thử search Title trước (nhanh hơn)
                    newsList = db.Database.SqlQuery<NewsListItem>(searchSql,
                        new System.Data.SqlClient.SqlParameter("@pageSize", pageSize + 1),
                        new System.Data.SqlClient.SqlParameter("@search", searchTerm),
                        new System.Data.SqlClient.SqlParameter("@lastId", (object)lastId ?? DBNull.Value)
                    ).ToList();

                    // Nếu ít kết quả, search cả Summary
                    if (newsList.Count < pageSize / 2)
                    {
                        var additionalResults = db.Database.SqlQuery<NewsListItem>(searchSummarySQL,
                            new System.Data.SqlClient.SqlParameter("@pageSize", pageSize + 1),
                            new System.Data.SqlClient.SqlParameter("@search", searchTerm),
                            new System.Data.SqlClient.SqlParameter("@lastId", (object)lastId ?? DBNull.Value)
                        ).ToList();

                        // Merge và remove duplicates
                        var existingIds = newsList.Select(n => n.Id).ToHashSet();
                        newsList.AddRange(additionalResults.Where(n => !existingIds.Contains(n.Id)));
                        newsList = newsList.Take(pageSize + 1).ToList();
                    }
                }
                else if (categoryId.HasValue)
                {
                    // 🚀 Tận dụng IDX_NewsCategory_CategoryId có sẵn
                    var categorySql = @"
                SELECT TOP (@pageSize) 
                    n.Id, n.Title, n.Summary, n.CreatedDate, n.Status, n.Ordering,
                    STUFF((SELECT ', ' + c.Name 
                           FROM NewsCategory nc2 WITH (NOLOCK)
                           INNER JOIN Category c WITH (NOLOCK) ON nc2.CategoryId = c.Id 
                           WHERE nc2.NewsId = n.Id 
                           FOR XML PATH('')), 1, 2, '') as CategoryNames
                FROM News n WITH (NOLOCK)
                INNER JOIN NewsCategory nc WITH (NOLOCK) ON n.Id = nc.NewsId
                WHERE n.Status = 1 
                AND nc.CategoryId = @categoryId  -- Sẽ dùng IDX_NewsCategory_CategoryId
                AND (@lastId IS NULL OR n.Id < @lastId)
                ORDER BY n.Id DESC";

                    newsList = db.Database.SqlQuery<NewsListItem>(categorySql,
                        new System.Data.SqlClient.SqlParameter("@pageSize", pageSize + 1),
                        new System.Data.SqlClient.SqlParameter("@categoryId", categoryId.Value),
                        new System.Data.SqlClient.SqlParameter("@lastId", (object)lastId ?? DBNull.Value)
                    ).ToList();

                    ViewBag.SelectedCategory = db.Categories.Find(categoryId.Value);
                }
                else
                {
                    // 🚀 Tận dụng IX_News_Status_Id có sẵn (hoặc Enhanced nếu đã tạo)
                    var allNewsSql = @"
                SELECT TOP (@pageSize) 
                    n.Id, n.Title, n.Summary, n.CreatedDate, n.Status, n.Ordering,
                    ISNULL(c_names.CategoryNames, '') as CategoryNames
                FROM News n WITH (NOLOCK)
                OUTER APPLY (
                    SELECT STUFF((SELECT ', ' + c.Name 
                                 FROM NewsCategory nc2 WITH (NOLOCK)
                                 INNER JOIN Category c WITH (NOLOCK) ON nc2.CategoryId = c.Id 
                                 WHERE nc2.NewsId = n.Id 
                                 FOR XML PATH('')), 1, 2, '') as CategoryNames
                ) c_names
                WHERE n.Status = 1   -- Sẽ dùng IX_News_Status_Id
                AND (@lastId IS NULL OR n.Id < @lastId)
                ORDER BY n.Id DESC";

                    newsList = db.Database.SqlQuery<NewsListItem>(allNewsSql,
                        new System.Data.SqlClient.SqlParameter("@pageSize", pageSize + 1),
                        new System.Data.SqlClient.SqlParameter("@lastId", (object)lastId ?? DBNull.Value)
                    ).ToList();
                }

                // Check for next page
                if (newsList.Count > pageSize)
                {
                    hasNextPage = true;
                    nextLastId = newsList[pageSize - 1].Id;
                    newsList.RemoveAt(pageSize);
                }

                stopwatch.Stop();

                System.Diagnostics.Debug.WriteLine($"🚀 Query executed in {stopwatch.ElapsedMilliseconds}ms using existing indexes");

                // Set ViewBag data
                ViewBag.Categories = new SelectList(db.Categories.Where(c => c.Status).OrderBy(c => c.Name), "Id", "Name", categoryId);
                ViewBag.CategoryId = categoryId;
                ViewBag.SearchTerm = search;
                ViewBag.HasNextPage = hasNextPage;
                ViewBag.NextLastId = nextLastId;
                ViewBag.HasPrevious = lastId.HasValue;
                ViewBag.TotalCount = newsList.Count;
                ViewBag.QueryTime = stopwatch.ElapsedMilliseconds;

                return View(newsList);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in News.Index: {ex.Message}");
                ViewBag.ErrorMessage = "Có lỗi xảy ra khi tải dữ liệu.";
                return View(new List<NewsListItem>());
            }
        }

        // 🚀 Method load CategoryNames riêng - NHANH HƠN
        private void LoadCategoryNamesForNews(List<NewsListItem> newsList)
        {
            if (!newsList.Any()) return;

            var newsIds = newsList.Select(n => n.Id).ToList();
            var newsIdParams = string.Join(",", newsIds);

            // Query categories cho tất cả news cùng lúc
            var categoriesSql = $@"
        SELECT nc.NewsId, c.Name
        FROM NewsCategory nc WITH (NOLOCK)
        INNER JOIN Category c WITH (NOLOCK) ON nc.CategoryId = c.Id
        WHERE nc.NewsId IN ({newsIdParams})
        ORDER BY nc.NewsId, c.Name";

            var categoryData = db.Database.SqlQuery<NewsCategory>(categoriesSql).ToList();

            // Group by NewsId
            var categoryLookup = categoryData.GroupBy(c => c.NewsId)
                                           .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(c => c.Name)));

            // Assign to news items
            foreach (var news in newsList)
            {
                news.CategoryNames = categoryLookup.ContainsKey(news.Id)
                                   ? categoryLookup[news.Id]
                                   : "Chưa phân loại";
            }
        }

        // 🚀 Cache Categories dropdown - KHÔNG QUERY MỖI LẦN
        private static List<Category> _cachedCategories = null;
        private static DateTime _cacheTime = DateTime.MinValue;

        private SelectList GetCachedCategories(int? selectedValue)
        {
            // Cache 10 phút
            if (_cachedCategories == null || DateTime.Now.Subtract(_cacheTime).TotalMinutes > 10)
            {
                _cachedCategories = db.Database.SqlQuery<Category>(
                    "SELECT Id, Name FROM Category WITH (NOLOCK) WHERE Status = 1 ORDER BY Name"
                ).ToList();
                _cacheTime = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"🔄 Categories cache refreshed: {_cachedCategories.Count} items");
            }

            return new SelectList(_cachedCategories, "Id", "Name", selectedValue);
        }

        // Helper class cho category data
        public class NewsCategory
        {
            public int NewsId { get; set; }
            public string Name { get; set; }
        }
        [HttpGet]
        public ActionResult QuickSearch(string term, int page = 1, int maxResults = 10)
        {
            try
            {
                if (string.IsNullOrEmpty(term) || term.Length < 2)
                {
                    return Json(new { success = true, results = new List<object>(), totalCount = 0 }, JsonRequestBehavior.AllowGet);
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // 🚀 SIÊU ĐơN GIẢN - KHÔNG LOAD GÌ THỪA
                var sql = @"
            SELECT TOP (@maxResults) Id, Title, Summary, CreatedDate
            FROM News WITH (NOLOCK)
            WHERE Status = 1 
            AND (Title LIKE @term OR Summary LIKE @term)
            ORDER BY 
                CASE WHEN Title LIKE @exactTerm THEN 1 ELSE 2 END,
                Id DESC";

                var searchTerm = "%" + term + "%";
                var exactTerm = term + "%";  // Starts with term

                var rawResults = db.Database.SqlQuery<SimpleNewsItem>(sql,
                    new System.Data.SqlClient.SqlParameter("@maxResults", maxResults),
                    new System.Data.SqlClient.SqlParameter("@term", searchTerm),
                    new System.Data.SqlClient.SqlParameter("@exactTerm", exactTerm)
                ).ToList();

                var results = rawResults.Select(n => new
                {
                    Id = n.Id,
                    Title = n.Title ?? "",
                    Summary = n.Summary != null && n.Summary.Length > 100
                             ? n.Summary.Substring(0, 100) + "..."
                             : (n.Summary ?? ""),
                    CreatedDate = n.CreatedDate.ToString("dd/MM/yyyy")
                }).ToList();

                stopwatch.Stop();
                System.Diagnostics.Debug.WriteLine($"🔍 QuickSearch '{term}' took {stopwatch.ElapsedMilliseconds}ms");

                return Json(new
                {
                    success = true,
                    results = results,
                    totalCount = results.Count,
                    queryTime = stopwatch.ElapsedMilliseconds
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult GetCategoryNames(int[] newsIds)
        {
            try
            {
                var newsIdParams = string.Join(",", newsIds);
                var categoriesSql = $@"
            SELECT nc.NewsId, c.Name
            FROM NewsCategory nc WITH (NOLOCK)
            INNER JOIN Category c WITH (NOLOCK) ON nc.CategoryId = c.Id
            WHERE nc.NewsId IN ({newsIdParams})";

                var categoryData = db.Database.SqlQuery<NewsCategory>(categoriesSql).ToList();
                var result = categoryData.GroupBy(c => c.NewsId)
                                        .ToDictionary(g => g.Key.ToString(), g => string.Join(", ", g.Select(c => c.Name)));

                return Json(new { success = true, categories = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: News/Details/5
        public ActionResult Details(int id)
        {
            News news = db.News.Include(n => n.Categories).FirstOrDefault(n => n.Id == id);
            if (news == null)
            {
                return HttpNotFound();
            }
            return View(news);
        }

        // GET: News/Create
        public ActionResult Create()
        {
            ViewBag.Categories = GetCategoriesCheckboxList();
            return View();
        }

        // POST: News/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Title,Summary,Content,Ordering,Status")] News news, int[] selectedCategories)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Kiểm tra danh mục
                    if (selectedCategories == null || selectedCategories.Length == 0)
                    {
                        ModelState.AddModelError("", "Vui lòng chọn ít nhất một danh mục cho tin tức.");
                        ViewBag.Categories = GetCategoriesCheckboxList(selectedCategories);
                        return View(news);
                    }

                    news.CreatedDate = DateTime.Now;

                    // Thêm tin tức vào database trước
                    db.News.Add(news);
                    db.SaveChanges();

                    // Thêm quan hệ với danh mục
                    var categories = db.Categories.Where(c => selectedCategories.Contains(c.Id)).ToList();
                    foreach (var category in categories)
                    {
                        news.Categories.Add(category);
                    }
                    db.SaveChanges();

                    TempData["Success"] = "Thêm tin tức thành công!";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Có lỗi xảy ra khi lưu tin tức: " + ex.Message);
            }

            ViewBag.Categories = GetCategoriesCheckboxList(selectedCategories);
            return View(news);
        }

        // GET: News/Edit/5
        // GET: News/Edit/5
        public ActionResult Edit(int id)
        {
            News news = db.News.Include(n => n.Categories).FirstOrDefault(n => n.Id == id);
            if (news == null)
            {
                return HttpNotFound();
            }

            // Load all categories and mark selected ones
            var allCategories = db.Categories.Where(c => c.Status).OrderBy(c => c.Name).ToList();
            var selectedCategoryIds = news.Categories.Select(c => c.Id).ToList();

            var categoryCheckboxList = allCategories.Select(c => new CategoryCheckboxViewModel
            {
                Id = c.Id,
                Name = c.Name,
                IsSelected = selectedCategoryIds.Contains(c.Id)
            }).ToList();

            ViewBag.Categories = categoryCheckboxList;
            ViewBag.SelectedCategoryIds = selectedCategoryIds; // For JavaScript initialization

            return View(news);
        }

        // POST: News/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,Title,Summary,Content,CreatedDate,Ordering,Status")] News news, int[] selectedCategories)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Kiểm tra danh mục
                    if (selectedCategories == null || selectedCategories.Length == 0)
                    {
                        ModelState.AddModelError("", "Vui lòng chọn ít nhất một danh mục cho tin tức.");

                        // Reload categories for view
                        var allCategories = db.Categories.Where(c => c.Status).OrderBy(c => c.Name).ToList();
                        ViewBag.Categories = allCategories.Select(c => new CategoryCheckboxViewModel
                        {
                            Id = c.Id,
                            Name = c.Name,
                            IsSelected = false
                        }).ToList();

                        return View(news);
                    }

                    // Lấy tin tức hiện tại từ database
                    var existingNews = db.News.Include(n => n.Categories).FirstOrDefault(n => n.Id == news.Id);
                    if (existingNews == null)
                    {
                        return HttpNotFound();
                    }

                    // Cập nhật thông tin tin tức
                    existingNews.Title = news.Title;
                    existingNews.Summary = news.Summary;
                    existingNews.Content = news.Content;
                    existingNews.Ordering = news.Ordering;
                    existingNews.Status = news.Status;
                    // CreatedDate không thay đổi

                    // Xóa tất cả quan hệ danh mục cũ
                    existingNews.Categories.Clear();

                    // Thêm quan hệ danh mục mới
                    var categories = db.Categories.Where(c => selectedCategories.Contains(c.Id)).ToList();
                    foreach (var category in categories)
                    {
                        existingNews.Categories.Add(category);
                    }

                    db.SaveChanges();
                    TempData["Success"] = "Cập nhật tin tức thành công!";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Có lỗi xảy ra khi cập nhật tin tức: " + ex.Message);
            }

            // Reload categories if validation fails
            var allCategoriesReload = db.Categories.Where(c => c.Status).OrderBy(c => c.Name).ToList();
            ViewBag.Categories = allCategoriesReload.Select(c => new CategoryCheckboxViewModel
            {
                Id = c.Id,
                Name = c.Name,
                IsSelected = selectedCategories != null && selectedCategories.Contains(c.Id)
            }).ToList();

            return View(news);
        }

        // POST: News/DeleteConfirmed/5 - AJAX Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                News news = db.News.Include(n => n.Categories).FirstOrDefault(n => n.Id == id);
                if (news == null)
                {
                    TempData["Error"] = "Không tìm thấy tin tức cần xóa.";
                    return RedirectToAction("Index");
                }

                // Xóa quan hệ với danh mục
                news.Categories.Clear();

                // Xóa tin tức
                db.News.Remove(news);
                db.SaveChanges();

                TempData["Success"] = "Xóa tin tức thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi xóa tin tức: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // API: AJAX Delete - Alternative method for pure AJAX
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult DeleteNews(int id)
        {
            try
            {
                // Log để debug
                System.Diagnostics.Debug.WriteLine($"DeleteNews called with ID: {id}");

                // Kiểm tra ID hợp lệ
                if (id <= 0)
                {
                    return Json(new { success = false, message = "ID tin tức không hợp lệ." });
                }

                // Tìm tin tức
                News news = db.News.Include(n => n.Categories).FirstOrDefault(n => n.Id == id);
                if (news == null)
                {
                    System.Diagnostics.Debug.WriteLine($"News not found with ID: {id}");
                    return Json(new { success = false, message = "Không tìm thấy tin tức cần xóa." });
                }

                System.Diagnostics.Debug.WriteLine($"Found news: {news.Title}");

                // Xóa quan hệ với danh mục
                if (news.Categories != null && news.Categories.Count > 0)
                {
                    news.Categories.Clear();
                    System.Diagnostics.Debug.WriteLine("Cleared categories");
                }

                // Xóa tin tức
                db.News.Remove(news);
                int rowsAffected = db.SaveChanges();

                System.Diagnostics.Debug.WriteLine($"Rows affected: {rowsAffected}");

                return Json(new { success = true, message = "Xóa tin tức thành công!" });
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException dbEx)
            {
                System.Diagnostics.Debug.WriteLine($"Database error: {dbEx.Message}");
                if (dbEx.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner exception: {dbEx.InnerException.Message}");
                }
                return Json(new { success = false, message = "Lỗi cơ sở dữ liệu khi xóa tin tức." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"General error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // GET: News/Search
        public ActionResult Search(string q, int page = 1, int pageSize = 20)
        {
            if (string.IsNullOrEmpty(q))
            {
                return View("SearchResults", new List<News>());
            }

            var newsQuery = db.News.Include(n => n.Categories)
                .Where(n => n.Status && (
                    n.Title.Contains(q) ||
                    n.Summary.Contains(q) ||
                    n.Content.Contains(q)));

            var totalCount = newsQuery.Count();
            var newsList = newsQuery
                .OrderByDescending(n => n.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.SearchTerm = q;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount = totalCount;

            return View("SearchResults", newsList);
        }

        // API: Lấy tin tức theo danh mục (cho AJAX)
        [HttpGet]
        public JsonResult GetNewsByCategory(int categoryId, int page = 1, int pageSize = 10)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"NewsController.GetNewsByCategory called: categoryId={categoryId}");

                var newsQuery = db.News.Include(n => n.Categories)
                    .Where(n => n.Status && n.Categories.Any(c => c.Id == categoryId));

                var totalCount = newsQuery.Count();
                var newsList = newsQuery
                    .OrderByDescending(n => n.CreatedDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList()
                    .Select(n => new {
                        Id = n.Id,
                        Title = n.Title ?? "",
                        Summary = n.Summary != null && n.Summary.Length > 150
                                 ? n.Summary.Substring(0, 150) + "..."
                                 : (n.Summary ?? ""),
                        CreatedDate = n.CreatedDate.ToString("dd/MM/yyyy"),
                        Categories = n.Categories != null ? n.Categories.Select(c => c.Name ?? "").ToList() : new List<string>()
                    })
                    .ToList();

                return Json(new
                {
                    success = true,
                    data = newsList,
                    totalCount = totalCount,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    currentPage = page
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NewsController Error: {ex.Message}");
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Quick status toggle
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ToggleStatus(int id)
        {
            try
            {
                var news = db.News.Find(id);
                if (news == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy tin tức." });
                }

                news.Status = !news.Status;
                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    status = news.Status,
                    message = news.Status ? "Kích hoạt tin tức thành công!" : "Tạm dừng tin tức thành công!"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // API: Get news statistics
        [HttpGet]
        public JsonResult GetNewsStats()
        {
            try
            {
                var stats = new
                {
                    totalNews = db.News.Count(),
                    activeNews = db.News.Count(n => n.Status),
                    inactiveNews = db.News.Count(n => !n.Status),
                    todayNews = db.News.Count(n => n.CreatedDate.Date == DateTime.Today),
                    weekNews = db.News.Count(n => n.CreatedDate >= DateTime.Today.AddDays(-7)),
                    monthNews = db.News.Count(n => n.CreatedDate >= DateTime.Today.AddDays(-30))
                };

                return Json(new { success = true, stats = stats }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #region Helper Methods

        private List<CategoryCheckboxViewModel> GetCategoriesCheckboxList(int[] selectedCategories = null)
        {
            var categories = db.Categories.Where(c => c.Status).OrderBy(c => c.Name).ToList();
            var result = new List<CategoryCheckboxViewModel>();

            foreach (var category in categories)
            {
                result.Add(new CategoryCheckboxViewModel
                {
                    Id = category.Id,
                    Name = category.Name,
                    IsSelected = selectedCategories != null && selectedCategories.Contains(category.Id)
                });
            }

            return result;
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ViewModel cho checkbox danh mục
    public class CategoryCheckboxViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsSelected { get; set; }
    }

    // ViewModel cho thống kê
    public class NewsStatsViewModel
    {
        public int TotalNews { get; set; }
        public int ActiveNews { get; set; }
        public int InactiveNews { get; set; }
        public int TodayNews { get; set; }
        public int WeekNews { get; set; }
        public int MonthNews { get; set; }
    }
}