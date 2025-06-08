using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using NewsManagement.Models;

namespace NewsManagement.Controllers
{
    public class NewsController : Controller
    {
        private TinTucEntities db = new TinTucEntities();

        // GET: News
        public ActionResult Index(int? categoryId, string search, int page = 1, int pageSize = 20)
        {
            var newsQuery = db.News.Include(n => n.Categories).AsQueryable();

            // Lọc theo danh mục nếu có
            if (categoryId.HasValue)
            {
                newsQuery = newsQuery.Where(n => n.Categories.Any(c => c.Id == categoryId.Value));
                ViewBag.SelectedCategory = db.Categories.Find(categoryId.Value);
                ViewBag.CategoryId = categoryId;
            }

            // Tìm kiếm nếu có
            if (!string.IsNullOrEmpty(search))
            {
                newsQuery = newsQuery.Where(n =>
                    n.Title.Contains(search) ||
                    n.Summary.Contains(search) ||
                    n.Content.Contains(search));
                ViewBag.SearchTerm = search;
            }

            // Phân trang
            var totalCount = newsQuery.Count();
            var newsList = newsQuery
                .OrderByDescending(n => n.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Dữ liệu cho View
            ViewBag.Categories = new SelectList(db.Categories.Where(c => c.Status).OrderBy(c => c.Name), "Id", "Name", categoryId);
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;

            return View(newsList);
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
        public ActionResult Edit(int id)
        {
            News news = db.News.Include(n => n.Categories).FirstOrDefault(n => n.Id == id);
            if (news == null)
            {
                return HttpNotFound();
            }

            var selectedCategories = news.Categories.Select(c => c.Id).ToArray();
            ViewBag.Categories = GetCategoriesCheckboxList(selectedCategories);

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
                        ViewBag.Categories = GetCategoriesCheckboxList(selectedCategories);
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

            ViewBag.Categories = GetCategoriesCheckboxList(selectedCategories);
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
                News news = db.News.Include(n => n.Categories).FirstOrDefault(n => n.Id == id);
                if (news == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy tin tức cần xóa." });
                }

                // Xóa quan hệ với danh mục
                news.Categories.Clear();

                // Xóa tin tức
                db.News.Remove(news);
                db.SaveChanges();

                return Json(new { success = true, message = "Xóa tin tức thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra khi xóa tin tức: " + ex.Message });
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