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

        // API: Lấy danh mục cấp 1 + một số cấp 2 phổ biến
        [HttpGet]
        public JsonResult GetCategoriesTree()
        {
            try
            {
                // CHỈ lấy danh mục cấp 1 và một số cấp 2 có nhiều tin nhất
                var rootCategories = db.Categories
                    .Where(c => c.ParentId == null && c.Status)
                    .OrderBy(c => c.Ordering)
                    .Take(20) // Giới hạn chỉ 20 danh mục gốc đầu tiên
                    .ToList();

                var categoryTree = rootCategories.Select(c => new
                {
                    Id = (int)c.Id,
                    Name = c.Name ?? "",
                    NewsCount = GetTotalNewsCountInCategoryTree(c.Id),
                    HasChildren = db.Categories.Any(child => child.ParentId == c.Id && child.Status),
                    Children = GetTopSubcategories(c.Id, 5) // Chỉ lấy 5 danh mục con phổ biến nhất
                }).ToList();

                return Json(new { success = true, categories = categoryTree }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API MỚI: Load danh mục con khi click vào toggle
        [HttpGet]
        public JsonResult GetSubcategories(int parentId)
        {
            try
            {
                var subcategories = db.Categories
                    .Where(c => c.ParentId == parentId && c.Status)
                    .OrderBy(c => c.Ordering)
                    .Take(50) // Giới hạn tối đa 50 con mỗi lần
                    .ToList() // Execute query first
                    .Select(c => new
                    {
                        Id = (int)c.Id,
                        Name = c.Name ?? "",
                        NewsCount = db.News.Count(n => n.Status && n.Categories.Any(cat => cat.Id == c.Id)),
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

        // API: Tìm kiếm danh mục
        [HttpGet]
        public JsonResult SearchCategories(string term)
        {
            try
            {
                if (string.IsNullOrEmpty(term) || term.Length < 2)
                {
                    return Json(new { success = true, categories = new List<object>() }, JsonRequestBehavior.AllowGet);
                }

                var categories = db.Categories
                    .Where(c => c.Status && c.Name.Contains(term))
                    .OrderByDescending(c => db.News.Count(n => n.Categories.Any(cat => cat.Id == c.Id)))
                    .Take(20)
                    .ToList() // Execute query first
                    .Select(c => new
                    {
                        Id = (int)c.Id,
                        Name = c.Name ?? "",
                        NewsCount = db.News.Count(n => n.Status && n.Categories.Any(cat => cat.Id == c.Id)),
                        Path = GetCategoryPath(c.Id)
                    })
                    .ToList();

                return Json(new { success = true, categories = categories }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #region Helper Methods - Optimized

        private List<object> GetTopSubcategories(int parentId, int top = 10)
        {
            var children = db.Categories
                .Where(c => c.ParentId == parentId && c.Status)
                .OrderBy(c => c.Ordering)
                .Take(top)
                .ToList(); // Execute query first

            return children.Select(c => new
            {
                Id = (int)c.Id,
                Name = c.Name ?? "",
                NewsCount = db.News.Count(n => n.Status && n.Categories.Any(cat => cat.Id == c.Id)),
                HasChildren = db.Categories.Any(child => child.ParentId == c.Id && child.Status)
            }).Cast<object>().ToList();
        }

        private int GetTotalNewsCountInCategoryTree(int categoryId)
        {
            try
            {
                // Sử dụng phương pháp đơn giản hơn để tránh lỗi SQL phức tạp
                var categoryIds = GetAllCategoryIdsInTree(categoryId);
                return db.News.Count(n => n.Status && n.Categories.Any(c => categoryIds.Contains(c.Id)));
            }
            catch
            {
                // Fallback: chỉ đếm tin tức trực tiếp trong danh mục này
                return db.News.Count(n => n.Status && n.Categories.Any(c => c.Id == categoryId));
            }
        }

        private List<int> GetAllCategoryIdsInTree(int categoryId)
        {
            var result = new List<int> { categoryId };
            
            try
            {
                var children = db.Categories
                    .Where(c => c.ParentId == categoryId)
                    .Select(c => c.Id)
                    .ToList();

                foreach (var childId in children)
                {
                    result.AddRange(GetAllCategoryIdsInTree(childId));
                }
            }
            catch
            {
                // Nếu có lỗi, chỉ trả về category hiện tại
            }

            return result;
        }

        private string GetCategoryPath(int categoryId)
        {
            try
            {
                var path = new List<string>();
                var currentId = (int?)categoryId;

                while (currentId.HasValue)
                {
                    var category = db.Categories.Find(currentId.Value);
                    if (category == null) break;

                    path.Insert(0, category.Name ?? "");
                    currentId = category.ParentId;
                }

                return string.Join(" > ", path);
            }
            catch
            {
                return "Không xác định";
            }
        }

        #endregion

        // Các method khác giữ nguyên
        [HttpGet]
        public JsonResult GetRecentNews(int count = 20)
        {
            try
            {
                var recentNews = db.News
                    .Include(n => n.Categories)
                    .Where(n => n.Status)
                    .OrderByDescending(n => n.CreatedDate)
                    .Take(count)
                    .ToList()
                    .Select(n => new
                    {
                        Id = (int)n.Id,
                        Title = n.Title ?? "",
                        Summary = n.Summary != null && n.Summary.Length > 150
                                 ? n.Summary.Substring(0, 150) + "..."
                                 : (n.Summary ?? ""),
                        CreatedDate = n.CreatedDate.ToString("dd/MM/yyyy"),
                        Categories = n.Categories != null ? n.Categories.Select(c => c.Name ?? "").ToList() : new List<string>()
                    })
                    .ToList();

                return Json(new { success = true, news = recentNews }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

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
                    .ToList()
                    .Select(n => new
                    {
                        id = (int)n.Id,
                        title = n.Title ?? "",
                        summary = n.Summary != null && n.Summary.Length > 100
                                 ? n.Summary.Substring(0, 100) + "..."
                                 : (n.Summary ?? ""),
                        url = Url.Action("Details", "News", new { id = n.Id }),
                        Id = (int)n.Id,
                        Title = n.Title ?? "",
                        Summary = n.Summary ?? "",
                        CreatedDate = n.CreatedDate.ToString("dd/MM/yyyy"),
                        Categories = n.Categories != null ? n.Categories.Select(c => c.Name ?? "").ToList() : new List<string>()
                    })
                    .ToList();

                return Json(new { success = true, results = results }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

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