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
            if (ModelState.IsValid)
            {
                news.CreatedDate = DateTime.Now;

                // Thêm tin tức vào database trước
                db.News.Add(news);
                db.SaveChanges();

                // Thêm quan hệ với danh mục
                if (selectedCategories != null && selectedCategories.Length > 0)
                {
                    var categories = db.Categories.Where(c => selectedCategories.Contains(c.Id)).ToList();
                    foreach (var category in categories)
                    {
                        news.Categories.Add(category);
                    }
                    db.SaveChanges();
                }

                TempData["Success"] = "Thêm tin tức thành công!";
                return RedirectToAction("Index");
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
            if (ModelState.IsValid)
            {
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
                if (selectedCategories != null && selectedCategories.Length > 0)
                {
                    var categories = db.Categories.Where(c => selectedCategories.Contains(c.Id)).ToList();
                    foreach (var category in categories)
                    {
                        existingNews.Categories.Add(category);
                    }
                }

                db.SaveChanges();
                TempData["Success"] = "Cập nhật tin tức thành công!";
                return RedirectToAction("Index");
            }

            ViewBag.Categories = GetCategoriesCheckboxList(selectedCategories);
            return View(news);
        }

        // GET: News/Delete/5
        public ActionResult Delete(int id)
        {
            News news = db.News.Include(n => n.Categories).FirstOrDefault(n => n.Id == id);
            if (news == null)
            {
                return HttpNotFound();
            }
            return View(news);
        }

        // POST: News/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            News news = db.News.Include(n => n.Categories).FirstOrDefault(n => n.Id == id);
            if (news != null)
            {
                // Xóa quan hệ với danh mục
                news.Categories.Clear();

                // Xóa tin tức
                db.News.Remove(news);
                db.SaveChanges();

                TempData["Success"] = "Xóa tin tức thành công!";
            }
            return RedirectToAction("Index");
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
        #region Helper Methods

        [HttpGet]
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
}