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

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Hệ thống quản lý tin tức - Phiên bản 1.0";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Thông tin liên hệ";
            return View();
        }

        // API: Lấy tin tức mới nhất cho Dashboard
        [HttpGet]
        public JsonResult GetRecentNews(int count = 20)
        {
            try
            {
                var recentNews = db.News
                    .Include(n => n.Categories) // Eager loading
                    .Where(n => n.Status)
                    .OrderByDescending(n => n.CreatedDate)
                    .Take(count)
                    .ToList() // Chuyển về memory trước khi format
                    .Select(n => new
                    {
                        n.Id,
                        n.Title,
                        Summary = n.Summary != null && n.Summary.Length > 150
                                 ? n.Summary.Substring(0, 150) + "..."
                                 : n.Summary,
                        CreatedDate = n.CreatedDate.ToString("dd/MM/yyyy"), // Format ở memory
                        Categories = n.Categories != null ? n.Categories.Select(c => c.Name).ToList() : new List<string>()
                    })
                    .ToList();

                return Json(new { success = true, news = recentNews }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Tìm kiếm nhanh (cho trang chủ)
        [HttpGet]
        public JsonResult QuickSearch(string term, int maxResults = 20)
        {
            try
            {
                if (string.IsNullOrEmpty(term) || term.Length < 2)
                {
                    return Json(new { success = true, results = new List<object>() }, JsonRequestBehavior.AllowGet);
                }

                var results = db.News
                    .Include(n => n.Categories)
                    .Where(n => n.Status && (
                        n.Title.Contains(term) ||
                        n.Summary.Contains(term) ||
                        n.Content.Contains(term)))
                    .OrderByDescending(n => n.CreatedDate)
                    .Take(maxResults)
                    .ToList() // Đưa về memory trước
                    .Select(n => new
                    {
                        id = n.Id,
                        title = n.Title,
                        summary = n.Summary != null && n.Summary.Length > 100
                                 ? n.Summary.Substring(0, 100) + "..."
                                 : n.Summary,
                        url = Url.Action("Details", "News", new { id = n.Id }),
                        Id = n.Id, // Thêm để tương thích
                        Title = n.Title, // Thêm để tương thích
                        Summary = n.Summary, // Thêm để tương thích
                        CreatedDate = n.CreatedDate.ToString("dd/MM/yyyy"),
                        Categories = n.Categories != null ? n.Categories.Select(c => c.Name).ToList() : new List<string>()
                    })
                    .ToList();

                return Json(new { success = true, results = results }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Lấy danh mục dạng cây cho menu
        [HttpGet]
        public JsonResult GetCategoriesTree()
        {
            try
            {
                // Lấy tất cả danh mục cấp 1 (ParentId = null)
                var rootCategories = db.Categories
                    .Where(c => c.ParentId == null && c.Status)
                    .OrderBy(c => c.Ordering)
                    .ToList();

                var categoryTree = rootCategories.Select(c => new
                {
                    c.Id,
                    c.Name,
                    NewsCount = GetTotalNewsCountInCategoryTree(c.Id),
                    Children = GetChildCategoriesForApi(c.Id)
                }).ToList();

                return Json(new { success = true, categories = categoryTree }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Lấy thống kê cho Dashboard
        [HttpGet]
        public JsonResult GetStats()
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
                    todayNews = db.News.Count(n => n.CreatedDate >= today && n.CreatedDate < tomorrow),
                    totalActiveCategories = db.Categories.Count(c => c.Status)
                };

                return Json(stats, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #region Helper Methods

        private List<object> GetChildCategoriesForApi(int parentId)
        {
            var children = db.Categories
                .Where(c => c.ParentId == parentId && c.Status)
                .OrderBy(c => c.Ordering)
                .ToList();

            return children.Select(c => new
            {
                Id = c.Id,
                Name = c.Name,
                NewsCount = GetTotalNewsCountInCategoryTree(c.Id),
                Children = GetChildCategoriesForApi(c.Id) // Đệ quy cho cấp con
            }).Cast<object>().ToList();
        }

        private int GetTotalNewsCountInCategoryTree(int categoryId)
        {
            var categoryIds = GetAllCategoryIdsInTree(categoryId);
            return db.News.Count(n => n.Status && n.Categories.Any(c => categoryIds.Contains(c.Id)));
        }

        private List<int> GetAllCategoryIdsInTree(int categoryId)
        {
            var result = new List<int> { categoryId };
            var children = db.Categories.Where(c => c.ParentId == categoryId).Select(c => c.Id).ToList();

            foreach (var childId in children)
            {
                result.AddRange(GetAllCategoryIdsInTree(childId));
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
}