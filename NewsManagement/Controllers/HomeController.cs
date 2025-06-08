using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using NewsManagement.Models;

namespace NewsManagement.Controllers
{
    public class HomeController : Controller
    {
        private TinTucEntities db = new TinTucEntities();

        public ActionResult Index(int? categoryId, string term)
        {
            var categories = db.Categories
                .Where(c => c.ParentId == null && c.Status)
                .OrderBy(c => c.Ordering)
                .ThenBy(c => c.Name)
                .Take(100)
                .ToList();

            ViewBag.Categories = categories;

            if (!string.IsNullOrEmpty(term))
            {
                var results = db.News
                    .Where(n => n.Status && (
                        n.Title.Contains(term) ||
                        n.Summary.Contains(term) ||
                        n.Content.Contains(term)))
                    .OrderByDescending(n => n.CreatedDate)
                    .Take(50)
                    .ToList();

                ViewBag.SearchTerm = term;
                return View(results);
            }

            if (categoryId.HasValue)
            {
                var newsList = db.News
                    .Where(n => n.Status && n.Categories.Any(c => c.Id == categoryId.Value))
                    .OrderByDescending(n => n.CreatedDate)
                    .Take(20)
                    .ToList();

                ViewBag.SelectedCategory = db.Categories.Find(categoryId.Value);
                return View(newsList);
            }

            var recentNews = db.News
                .Where(n => n.Status)
                .OrderByDescending(n => n.CreatedDate)
                .Take(20)
                .ToList();

            return View(recentNews);
        }


        // API: Lấy danh mục cấp 1 - Tối ưu cho 5000 danh mục
        [HttpGet]
        public ActionResult GetCategoriesTree()
        {
            try
            {
                var rootCategories = db.Categories
                    .Where(c => c.ParentId == null && c.Status)
                    .OrderBy(c => c.Ordering)
                    .ThenBy(c => c.Name)
                    .Take(100) // Giới hạn để tăng tốc
                    .ToList();

                var categoryTree = rootCategories.Select(c => new
                {
                    Id = c.Id,
                    Name = c.Name ?? "",
                    NewsCount = GetDirectNewsCount(c.Id),
                    HasChildren = db.Categories.Any(child => child.ParentId == c.Id && child.Status)
                }).ToList();

                return Json(new { success = true, categories = categoryTree }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetCategoriesTree: {ex.Message}");
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Load danh mục con khi click expand (Lazy Loading)
        [HttpGet]
        public ActionResult GetSubcategories(int parentId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"GetSubcategories called with parentId: {parentId}");

                var subcategories = db.Categories
                    .Where(c => c.ParentId == parentId && c.Status)
                    .OrderBy(c => c.Ordering)
                    .ThenBy(c => c.Name)
                    .Take(50) // Giới hạn để tăng tốc
                    .ToList()
                    .Select(c => new
                    {
                        Id = c.Id,
                        Name = c.Name ?? "",
                        NewsCount = GetDirectNewsCount(c.Id),
                        HasChildren = db.Categories.Any(child => child.ParentId == c.Id && child.Status)
                    })
                    .ToList();

                return Json(new { success = true, subcategories = subcategories }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetSubcategories: {ex.Message}");
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Lấy tin tức theo danh mục - Tối ưu cho 50000 tin tức
        [HttpGet]
        public ActionResult GetNewsByCategory(int categoryId, int page = 1, int pageSize = 20)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"GetNewsByCategory: categoryId={categoryId}, page={page}");

                // Kiểm tra danh mục có tồn tại
                var category = db.Categories.Find(categoryId);
                if (category == null)
                {
                    return Json(new { success = false, message = "Danh mục không tồn tại" }, JsonRequestBehavior.AllowGet);
                }

                // Lấy tin tức trực tiếp trong danh mục (không bao gồm danh mục con để tăng tốc)
                var newsQuery = db.News
                    .Where(n => n.Status && n.Categories.Any(c => c.Id == categoryId))
                    .OrderByDescending(n => n.CreatedDate);

                var totalCount = newsQuery.Count();

                var newsList = newsQuery
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList()
                    .Select(n => new
                    {
                        Id = n.Id,
                        Title = n.Title ?? "",
                        Summary = n.Summary != null && n.Summary.Length > 200
                                 ? n.Summary.Substring(0, 200) + "..."
                                 : (n.Summary ?? ""),
                        CreatedDate = n.CreatedDate.ToString("dd/MM/yyyy")
                    })
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"Found {newsList.Count} news items for category {categoryId}");

                return Json(new
                {
                    success = true,
                    data = newsList,
                    totalCount = totalCount,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    currentPage = page,
                    categoryName = category.Name
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetNewsByCategory: {ex.Message}");
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Tìm kiếm tin tức - Tối ưu với phân trang
        [HttpGet]
        public ActionResult QuickSearch(string term, int page = 1, int maxResults = 20)
        {
            try
            {
                if (string.IsNullOrEmpty(term) || term.Length < 2)
                {
                    return Json(new { success = true, results = new List<object>(), totalCount = 0 }, JsonRequestBehavior.AllowGet);
                }

                var query = db.News
                    .Where(n => n.Status && (
                        n.Title.Contains(term) ||
                        n.Summary.Contains(term) ||
                        n.Content.Contains(term)));

                var totalCount = query.Count();
                var results = query
                    .OrderByDescending(n => n.CreatedDate)
                    .Skip((page - 1) * maxResults)
                    .Take(maxResults)
                    .ToList()
                    .Select(n => new
                    {
                        Id = n.Id,
                        Title = n.Title ?? "",
                        Summary = n.Summary != null && n.Summary.Length > 150
                                 ? n.Summary.Substring(0, 150) + "..."
                                 : (n.Summary ?? ""),
                        CreatedDate = n.CreatedDate.ToString("dd/MM/yyyy"),
                        url = Url.Action("Details", "News", new { id = n.Id })
                    })
                    .ToList();

                return Json(new
                {
                    success = true,
                    results = results,
                    totalCount = totalCount,
                    currentPage = page,
                    totalPages = (int)Math.Ceiling((double)totalCount / maxResults)
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Lấy tin tức mới nhất
        [HttpGet]
        public ActionResult GetRecentNews(int count = 20)
        {
            try
            {
                var recentNews = db.News
                    .Where(n => n.Status)
                    .OrderByDescending(n => n.CreatedDate)
                    .Take(count)
                    .ToList()
                    .Select(n => new
                    {
                        Id = n.Id,
                        Title = n.Title ?? "",
                        Summary = n.Summary != null && n.Summary.Length > 150
                                 ? n.Summary.Substring(0, 150) + "..."
                                 : (n.Summary ?? ""),
                        CreatedDate = n.CreatedDate.ToString("dd/MM/yyyy")
                    })
                    .ToList();

                return Json(new { success = true, news = recentNews }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Tìm kiếm danh mục với phân trang
        [HttpGet]
        public ActionResult SearchCategories(string term, int page = 1, int pageSize = 20)
        {
            try
            {
                if (string.IsNullOrEmpty(term) || term.Length < 2)
                {
                    return Json(new { success = true, categories = new List<object>(), totalCount = 0 }, JsonRequestBehavior.AllowGet);
                }

                var query = db.Categories
                    .Where(c => c.Status && c.Name.Contains(term));

                var totalCount = query.Count();
                var categories = query
                    .OrderBy(c => c.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList()
                    .Select(c => new
                    {
                        Id = c.Id,
                        Name = c.Name ?? "",
                        NewsCount = GetDirectNewsCount(c.Id),
                        Path = GetCategoryPath(c.Id)
                    })
                    .ToList();

                return Json(new
                {
                    success = true,
                    categories = categories,
                    totalCount = totalCount,
                    currentPage = page,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Lấy thống kê tổng quan
        [HttpGet]
        public ActionResult GetStats()
        {
            try
            {
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                var stats = new
                {
                    success = true,
                    totalNews = db.News.Count(),
                    activeNews = db.News.Count(n => n.Status),
                    totalCategories = db.Categories.Count(),
                    activeCategories = db.Categories.Count(c => c.Status),
                    todayNews = db.News.Count(n => n.CreatedDate >= today && n.CreatedDate < tomorrow)
                };

                return Json(stats, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #region Helper Methods - Tối ưu hóa

        private int GetDirectNewsCount(int categoryId)
        {
            try
            {
                return db.News.Count(n => n.Status && n.Categories.Any(c => c.Id == categoryId));
            }
            catch
            {
                return 0;
            }
        }

        private string GetCategoryPath(int categoryId)
        {
            try
            {
                var path = new List<string>();
                var currentId = (int?)categoryId;
                var maxDepth = 8; // Giới hạn độ sâu

                while (currentId.HasValue && maxDepth > 0)
                {
                    var category = db.Categories
                        .Where(c => c.Id == currentId.Value)
                        .Select(c => new { c.Name, c.ParentId })
                        .FirstOrDefault();

                    if (category == null) break;

                    path.Insert(0, category.Name ?? "");
                    currentId = category.ParentId;
                    maxDepth--;
                }

                return string.Join(" > ", path);
            }
            catch
            {
                return "Không xác định";
            }
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
}