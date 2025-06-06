using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using PagedList;
using NewsManagement.Models;

namespace NewsManagement.Controllers
{
    public class CategoryController : Controller
    {
        private TinTucEntities db = new TinTucEntities();

        // GET: Category - Với phân trang
        public ActionResult Index(int? page, int? parentId)
        {
            int pageSize = 50;
            int pageNumber = page ?? 1;

            var query = db.Categories.AsQueryable();

            if (parentId.HasValue)
            {
                query = query.Where(c => c.ParentId == parentId);
                ViewBag.ParentCategory = db.Categories.Find(parentId.Value);
            }
            else
            {
                query = query.Where(c => c.ParentId == null);
            }

            query = query.OrderBy(c => c.Ordering).ThenBy(c => c.Name);

            var pagedCategories = query.ToPagedList(pageNumber, pageSize);

            var categoryViewModels = pagedCategories.Select(c => new CategoryViewModel
            {
                Category = c,
                NewsCount = GetDirectNewsCount(c.Id),
                TotalNewsCount = GetTotalNewsCountInCategoryTree(c.Id),
                Level = GetCategoryLevel(c.Id),
                HasChildren = db.Categories.Any(child => child.ParentId == c.Id)
            }).ToList();

            ViewBag.PagedCategories = pagedCategories;
            ViewBag.ParentId = parentId;

            return View(categoryViewModels);
        }

        public ActionResult Create(int? parentId)
        {
            var categories = GetCategoriesForDropdownOptimized();
            ViewBag.ParentId = new SelectList(categories, "Id", "DisplayName", parentId);
            ViewBag.SelectedParentId = parentId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Name,Description,ParentId,Ordering,Status")] Category category)
        {
            if (ModelState.IsValid)
            {
                if (category.ParentId.HasValue && GetCategoryDepth(category.ParentId.Value) >= 8)
                {
                    ModelState.AddModelError("ParentId", "Không thể tạo danh mục quá 8 cấp.");
                    var categories = GetCategoriesForDropdownOptimized();
                    ViewBag.ParentId = new SelectList(categories, "Id", "DisplayName", category.ParentId);
                    return View(category);
                }

                db.Categories.Add(category);
                db.SaveChanges();
                TempData["Success"] = "Thêm danh mục thành công!";

                if (category.ParentId.HasValue)
                {
                    return RedirectToAction("Index", new { parentId = category.ParentId });
                }
                return RedirectToAction("Index");
            }

            var categoriesForDropdown = GetCategoriesForDropdownOptimized();
            ViewBag.ParentId = new SelectList(categoriesForDropdown, "Id", "DisplayName", category.ParentId);
            return View(category);
        }

        public ActionResult Edit(int id)
        {
            Category category = db.Categories.Find(id);
            if (category == null)
            {
                return HttpNotFound();
            }

            var availableParents = GetCategoriesForDropdownOptimized()
                .Where(c => c.Id != id && !IsDescendantOf(c.Id, id))
                .ToList();

            ViewBag.ParentId = new SelectList(availableParents, "Id", "DisplayName", category.ParentId);
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,Name,Description,ParentId,Ordering,Status")] Category category)
        {
            if (ModelState.IsValid)
            {
                if (category.ParentId.HasValue &&
                    (category.ParentId == category.Id || IsDescendantOf(category.ParentId.Value, category.Id)))
                {
                    ModelState.AddModelError("ParentId", "Không thể chọn chính nó hoặc danh mục con làm danh mục cha.");
                }
                else if (category.ParentId.HasValue && GetCategoryDepth(category.ParentId.Value) >= 8)
                {
                    ModelState.AddModelError("ParentId", "Không thể tạo danh mục quá 8 cấp.");
                }
                else
                {
                    db.Entry(category).State = EntityState.Modified;
                    db.SaveChanges();
                    TempData["Success"] = "Cập nhật danh mục thành công!";
                    return RedirectToAction("Index");
                }
            }

            var availableParents = GetCategoriesForDropdownOptimized()
                .Where(c => c.Id != category.Id && !IsDescendantOf(c.Id, category.Id))
                .ToList();

            ViewBag.ParentId = new SelectList(availableParents, "Id", "DisplayName", category.ParentId);
            return View(category);
        }

        public ActionResult Delete(int id)
        {
            Category category = db.Categories.Find(id);
            if (category == null)
            {
                return HttpNotFound();
            }

            var hasChildren = db.Categories.Any(c => c.ParentId == id);
            var hasNews = db.News.Any(n => n.Categories.Any(c => c.Id == id));

            ViewBag.HasChildren = hasChildren;
            ViewBag.HasNews = hasNews;
            ViewBag.NewsCount = GetTotalNewsCountInCategoryTree(id);

            return View(category);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Category category = db.Categories.Find(id);

            var hasChildren = db.Categories.Any(c => c.ParentId == id);
            var hasNews = db.News.Any(n => n.Categories.Any(c => c.Id == id));

            if (hasChildren)
            {
                TempData["Error"] = "Không thể xóa danh mục có danh mục con. Vui lòng xóa danh mục con trước.";
                return RedirectToAction("Delete", new { id = id });
            }

            if (hasNews)
            {
                TempData["Error"] = "Không thể xóa danh mục đang có tin tức. Vui lòng chuyển tin tức sang danh mục khác trước.";
                return RedirectToAction("Delete", new { id = id });
            }

            db.Categories.Remove(category);
            db.SaveChanges();
            TempData["Success"] = "Xóa danh mục thành công!";
            return RedirectToAction("Index");
        }

        public ActionResult NewsByCategory(int categoryId, int page = 1, int pageSize = 20)
        {
            var category = db.Categories.Find(categoryId);
            if (category == null)
            {
                return HttpNotFound();
            }

            var categoryIds = GetAllCategoryIdsInTreeOptimized(categoryId);

            var newsQuery = db.News
                .Where(n => n.Status && n.Categories.Any(c => categoryIds.Contains(c.Id)))
                .OrderByDescending(n => n.CreatedDate);

            var totalCount = newsQuery.Count();
            var newsList = newsQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(n => n.Categories)
                .ToList();

            ViewBag.Category = category;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;

            return View(newsList);
        }

        [HttpGet]
        public JsonResult SearchCategories(string term, int page = 1, int pageSize = 20)
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
                    .OrderByDescending(c => db.News.Count(n => n.Categories.Any(cat => cat.Id == c.Id)))
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

        #region Helper Methods

        private List<CategoryDropdownItem> GetCategoriesForDropdownOptimized()
        {
            var allCategories = db.Categories
                .Where(c => c.Status)
                .Select(c => new SimpleCategory { Id = c.Id, Name = c.Name, ParentId = c.ParentId })
                .OrderBy(c => c.Name)
                .ToList();

            var result = new List<CategoryDropdownItem>();

            foreach (var cat in allCategories)
            {
                var path = GetCategoryPathFromList(cat.Id, allCategories);
                result.Add(new CategoryDropdownItem
                {
                    Id = cat.Id,
                    Name = cat.Name,
                    DisplayName = path
                });
            }

            return result.OrderBy(c => c.DisplayName).ToList();
        }

        private string GetCategoryPathFromList(int categoryId, List<SimpleCategory> allCategories)
        {
            var path = new List<string>();
            var currentId = (int?)categoryId;

            while (currentId.HasValue)
            {
                var category = allCategories.FirstOrDefault(c => c.Id == currentId.Value);
                if (category == null) break;

                path.Insert(0, category.Name ?? "");
                currentId = category.ParentId;
            }

            return string.Join(" > ", path);
        }

        private List<int> GetAllCategoryIdsInTreeOptimized(int categoryId)
        {
            var result = new List<int> { categoryId };
            var queue = new Queue<int>();
            queue.Enqueue(categoryId);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                var childIds = db.Categories
                    .Where(c => c.ParentId == currentId)
                    .Select(c => c.Id)
                    .ToList();

                foreach (var childId in childIds)
                {
                    if (!result.Contains(childId))
                    {
                        result.Add(childId);
                        queue.Enqueue(childId);
                    }
                }
            }

            return result;
        }

        private int GetDirectNewsCount(int categoryId)
        {
            return db.News.Count(n => n.Status && n.Categories.Any(c => c.Id == categoryId));
        }

        private int GetTotalNewsCountInCategoryTree(int categoryId)
        {
            var categoryIds = GetAllCategoryIdsInTreeOptimized(categoryId);
            return db.News.Count(n => n.Status && n.Categories.Any(c => categoryIds.Contains(c.Id)));
        }

        private int GetCategoryLevel(int categoryId)
        {
            var level = 1;
            var currentId = db.Categories.Where(c => c.Id == categoryId).Select(c => c.ParentId).FirstOrDefault();

            while (currentId.HasValue)
            {
                level++;
                currentId = db.Categories.Where(c => c.Id == currentId.Value).Select(c => c.ParentId).FirstOrDefault();
            }

            return level;
        }

        private int GetCategoryDepth(int categoryId)
        {
            var category = db.Categories.Find(categoryId);
            if (category == null || !category.ParentId.HasValue)
                return 1;

            return 1 + GetCategoryDepth(category.ParentId.Value);
        }

        private bool IsDescendantOf(int childId, int ancestorId)
        {
            var child = db.Categories.Find(childId);
            if (child == null || !child.ParentId.HasValue)
                return false;

            if (child.ParentId == ancestorId)
                return true;

            return IsDescendantOf(child.ParentId.Value, ancestorId);
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class CategoryViewModel
    {
        public Category Category { get; set; }
        public int NewsCount { get; set; }
        public int TotalNewsCount { get; set; }
        public int Level { get; set; }
        public bool HasChildren { get; set; }
    }

    public class CategoryDropdownItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
    }

    public class SimpleCategory
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? ParentId { get; set; }
    }
}
