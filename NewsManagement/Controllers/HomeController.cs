using System;
using System.Collections.Generic;
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

        // API: Lấy tin tức mới nhất cho Dashboard
        [HttpGet]
        public JsonResult GetRecentNews(int count = 5)
        {
            try
            {
                var recentNews = db.News
                    .Where(n => n.Status)
                    .OrderByDescending(n => n.CreatedDate)
                    .Take(count)
                    .Select(n => new
                    {
                        n.Id,
                        n.Title,
                        Summary = n.Summary != null && n.Summary.Length > 100
                                 ? n.Summary.Substring(0, 100) + "..."
                                 : n.Summary,
                        CreatedDate = n.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                        Categories = n.Categories.Select(c => c.Name).ToList()
                    })
                    .ToList();

                return Json(new { success = true, news = recentNews }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Lấy danh mục có nhiều tin tức nhất
        [HttpGet]
        public JsonResult GetTopCategories(int count = 5)
        {
            try
            {
                var topCategories = db.Categories
                    .Where(c => c.Status)
                    .Select(c => new
                    {
                        c.Id,
                        c.Name,
                        NewsCount = c.News.Count(n => n.Status)
                    })
                    .OrderByDescending(c => c.NewsCount)
                    .Take(count)
                    .ToList();

                return Json(new { success = true, categories = topCategories }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Thống kê tin tức theo tháng (12 tháng gần nhất)
        [HttpGet]
        public JsonResult GetNewsStatsByMonth()
        {
            try
            {
                var startDate = DateTime.Now.AddMonths(-11).Date;
                var endDate = DateTime.Now.Date.AddDays(1);

                var monthlyStats = new List<object>();

                for (int i = 0; i < 12; i++)
                {
                    var monthStart = startDate.AddMonths(i);
                    var monthEnd = monthStart.AddMonths(1);

                    var newsCount = db.News.Count(n =>
                        n.CreatedDate >= monthStart &&
                        n.CreatedDate < monthEnd);

                    monthlyStats.Add(new
                    {
                        month = monthStart.ToString("MM/yyyy"),
                        count = newsCount
                    });
                }

                return Json(new { success = true, data = monthlyStats }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Tìm kiếm nhanh (cho autocomplete)
        [HttpGet]
        public JsonResult QuickSearch(string term, int maxResults = 10)
        {
            try
            {
                if (string.IsNullOrEmpty(term) || term.Length < 2)
                {
                    return Json(new { success = true, results = new List<object>() }, JsonRequestBehavior.AllowGet);
                }

                var results = db.News
                    .Where(n => n.Status && (
                        n.Title.Contains(term) ||
                        n.Summary.Contains(term)))
                    .OrderByDescending(n => n.CreatedDate)
                    .Take(maxResults)
                    .Select(n => new
                    {
                        id = n.Id,
                        title = n.Title,
                        summary = n.Summary != null && n.Summary.Length > 50
                                 ? n.Summary.Substring(0, 50) + "..."
                                 : n.Summary,
                        url = Url.Action("Details", "News", new { id = n.Id })
                    })
                    .ToList();

                return Json(new { success = true, results = results }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

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