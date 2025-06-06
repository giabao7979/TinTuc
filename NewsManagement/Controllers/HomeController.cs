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

        // API: Lấy danh mục cấp 1
        [HttpGet]
        public ActionResult GetCategoriesTree()
        {
            try
            {
                var rootCategories = db.Categories
                    .Where(c => c.ParentId == null && c.Status)
                    .OrderBy(c => c.Ordering)
                    .ThenBy(c => c.Name)
                    .Take(100)
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
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Load danh mục con
        [HttpGet]
        public ActionResult GetSubcategories(int parentId)
        {
            try
            {
                var subcategories = db.Categories
                    .Where(c => c.ParentId == parentId && c.Status)
                    .OrderBy(c => c.Ordering)
                    .ThenBy(c => c.Name)
                    .Take(50)
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
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Lấy tin tức theo danh mục
        [HttpGet]
        public ActionResult GetNewsByCategory(int categoryId, int page = 1, int pageSize = 20)
        {
            try
            {
                // Kiểm tra danh mục có tồn tại không
                var category = db.Categories.Find(categoryId);
                if (category == null)
                {
                    return Json(new { success = false, message = "Danh mục không tồn tại" }, JsonRequestBehavior.AllowGet);
                }

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
                        Summary = n.Summary != null && n.Summary.Length > 150
                                 ? n.Summary.Substring(0, 150) + "..."
                                 : (n.Summary ?? ""),
                        CreatedDate = n.CreatedDate.ToString("dd/MM/yyyy")
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
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Tìm kiếm tin tức
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
                        id = n.Id,
                        title = n.Title ?? "",
                        summary = n.Summary != null && n.Summary.Length > 100
                                 ? n.Summary.Substring(0, 100) + "..."
                                 : (n.Summary ?? ""),
                        url = Url.Action("Details", "News", new { id = n.Id }),
                        Id = n.Id,
                        Title = n.Title ?? "",
                        Summary = n.Summary ?? "",
                        CreatedDate = n.CreatedDate.ToString("dd/MM/yyyy")
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

        #region Helper Methods

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