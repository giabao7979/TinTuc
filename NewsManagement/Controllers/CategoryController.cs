using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using NewsManagement.Models;

namespace NewsManagement.Controllers
{
    public class CategoryController : Controller
    {
        private TinTucEntities db = new TinTucEntities();

        // GET: Category
        public ActionResult Index()
        {
            // Lấy tất cả danh mục cấp 1 (ParentId = null)
            var rootCategories = db.Categories
                .Where(c => c.ParentId == null)
                .OrderBy(c => c.Ordering)
                .ToList();

            var categoryViewModels = rootCategories.Select(c => new CategoryViewModel
            {
                Category = c,
                NewsCount = GetTotalNewsCountInCategoryTree(c.Id),
                Level = 0,
                Children = GetChildCategories(c.Id, 1)
            }).ToList();

            return View(categoryViewModels);
        }

        // GET: Category/Create
        public ActionResult Create(int? parentId)
        {
            ViewBag.ParentId = new SelectList(GetCategoriesForDropdown(), "Id", "Name", parentId);
            ViewBag.SelectedParentId = parentId;
            return View();
        }

        // POST: Category/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Name,Description,ParentId,Ordering,Status")] Category category)
        {
            if (ModelState.IsValid)
            {
                // Kiểm tra độ sâu không quá 8 cấp
                if (category.ParentId.HasValue && GetCategoryDepth(category.ParentId.Value) >= 8)
                {
                    ModelState.AddModelError("ParentId", "Không thể tạo danh mục quá 8 cấp.");
                    ViewBag.ParentId = new SelectList(GetCategoriesForDropdown(), "Id", "Name", category.ParentId);
                    return View(category);
                }

                db.Categories.Add(category);
                db.SaveChanges();
                TempData["Success"] = "Thêm danh mục thành công!";
                return RedirectToAction("Index");
            }

            ViewBag.ParentId = new SelectList(GetCategoriesForDropdown(), "Id", "Name", category.ParentId);
            return View(category);
        }

        // GET: Category/Edit/5
        public ActionResult Edit(int id)
        {
            Category category = db.Categories.Find(id);
            if (category == null)
            {
                return HttpNotFound();
            }

            // Lấy danh sách danh mục cho dropdown, loại trừ chính nó và các danh mục con
            var availableParents = GetCategoriesForDropdown()
                .Where(c => c.Id != id && !IsDescendantOf(c.Id, id))
                .ToList();

            ViewBag.ParentId = new SelectList(availableParents, "Id", "Name", category.ParentId);
            return View(category);
        }

        // POST: Category/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,Name,Description,ParentId,Ordering,Status")] Category category)
        {
            if (ModelState.IsValid)
            {
                // Kiểm tra không tạo vòng lặp
                if (category.ParentId.HasValue &&
                    (category.ParentId == category.Id || IsDescendantOf(category.ParentId.Value, category.Id)))
                {
                    ModelState.AddModelError("ParentId", "Không thể chọn chính nó hoặc danh mục con làm danh mục cha.");
                }
                // Kiểm tra độ sâu
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

            var availableParents = GetCategoriesForDropdown()
                .Where(c => c.Id != category.Id && !IsDescendantOf(c.Id, category.Id))
                .ToList();

            ViewBag.ParentId = new SelectList(availableParents, "Id", "Name", category.ParentId);
            return View(category);
        }

        // GET: Category/Delete/5
        public ActionResult Delete(int id)
        {
            Category category = db.Categories.Find(id);
            if (category == null)
            {
                return HttpNotFound();
            }

            // Kiểm tra xem có danh mục con không
            var hasChildren = db.Categories.Any(c => c.ParentId == id);
            var hasNews = db.News.Any(n => n.Categories.Any(c => c.Id == id));

            ViewBag.HasChildren = hasChildren;
            ViewBag.HasNews = hasNews;
            ViewBag.NewsCount = GetTotalNewsCountInCategoryTree(id);

            return View(category);
        }

        // POST: Category/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Category category = db.Categories.Find(id);

            // Kiểm tra ràng buộc
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

        // API: Lấy tin tức theo danh mục
        public ActionResult NewsByCategory(int categoryId, int page = 1, int pageSize = 10)
        {
            var category = db.Categories.Find(categoryId);
            if (category == null)
            {
                return HttpNotFound();
            }

            // Lấy tất cả ID danh mục con
            var categoryIds = GetAllCategoryIdsInTree(categoryId);

            var newsQuery = db.News
                .Where(n => n.Status && n.Categories.Any(c => categoryIds.Contains(c.Id)))
                .OrderByDescending(n => n.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);

            var newsList = newsQuery.ToList();
            var totalCount = db.News.Count(n => n.Status && n.Categories.Any(c => categoryIds.Contains(c.Id)));

            ViewBag.Category = category;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount = totalCount;

            return View(newsList);
        }

        #region Helper Methods

        private List<CategoryViewModel> GetChildCategories(int parentId, int level)
        {
            if (level >= 8) return new List<CategoryViewModel>(); // Giới hạn 8 cấp

            var children = db.Categories
                .Where(c => c.ParentId == parentId)
                .OrderBy(c => c.Ordering)
                .ToList();

            return children.Select(c => new CategoryViewModel
            {
                Category = c,
                NewsCount = GetTotalNewsCountInCategoryTree(c.Id),
                Level = level,
                Children = GetChildCategories(c.Id, level + 1)
            }).ToList();
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

        private List<Category> GetCategoriesForDropdown()
        {
            return db.Categories.Where(c => c.Status).OrderBy(c => c.Name).ToList();
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

    // ViewModel cho hiển thị cây danh mục
    public class CategoryViewModel
    {
        public Category Category { get; set; }
        public int NewsCount { get; set; }
        public int Level { get; set; }
        public List<CategoryViewModel> Children { get; set; } = new List<CategoryViewModel>();
    }
}