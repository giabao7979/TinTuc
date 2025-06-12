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
        private TinTuc_DEntities db = new TinTuc_DEntities();

        public ActionResult Index(
            int? categoryId,
            string searchTerm,
            string categorySearch,
            int page = 1,
            int pageSize = 12,
            string sortBy = "newest",
            int[] expanded = null,
            bool expandAll = false,
            bool collapseAll = false)
        {
            var model = new HomeViewModel
            {
                SelectedCategoryId = categoryId,
                SearchTerm = searchTerm,
                CategorySearchTerm = categorySearch,
                CurrentPage = page,
                PageSize = pageSize,
                SortBy = sortBy
            };

            // Handle expand/collapse all
            var expandedIds = new List<int>();
            if (expanded != null)
            {
                expandedIds.AddRange(expanded);
            }

            if (expandAll)
            {
                expandedIds = GetAllCategoryIds();
            }
            else if (collapseAll)
            {
                expandedIds.Clear();
            }

            model.ExpandedCategoryIds = expandedIds;

            // Get selected category
            if (categoryId.HasValue)
            {
                model.SelectedCategory = db.Categories.Find(categoryId.Value);
            }

            // Handle category search
            if (!string.IsNullOrEmpty(categorySearch))
            {
                model.CategorySearchResults = SearchCategories(categorySearch);
            }
            else
            {
                // Build category tree
                model.Categories = BuildCategoryTree(expandedIds);
            }

            // Get news data
            LoadNewsData(model);

            return View(model);
        }

        private List<CategoryTreeNode> BuildCategoryTree(List<int> expandedIds)
        {
            // Create a simple DTO class to avoid dynamic issues
            var allCategories = db.Categories
                .Where(c => c.Status)
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    ParentId = c.ParentId,
                    NewsCount = c.News.Count(n => n.Status)
                })
                .ToList();

            var rootCategories = allCategories.Where(c => c.ParentId == null).ToList();
            var categoryTree = new List<CategoryTreeNode>();

            foreach (var root in rootCategories)
            {
                var node = new CategoryTreeNode
                {
                    Id = root.Id,
                    Name = root.Name,
                    Description = root.Description,
                    ParentId = root.ParentId,
                    NewsCount = root.NewsCount,
                    HasChildren = allCategories.Any(c => c.ParentId == root.Id),
                    Level = 0,
                    IsExpanded = expandedIds.Contains(root.Id)
                };

                if (node.IsExpanded)
                {
                    LoadChildCategories(node, allCategories, expandedIds, 1);
                }

                categoryTree.Add(node);
            }

            return categoryTree.OrderBy(c => c.Name).ToList();
        }

        private void LoadChildCategories(CategoryTreeNode parent, List<CategoryDto> allCategories, List<int> expandedIds, int level)
        {
            var children = allCategories.Where(c => c.ParentId == parent.Id).ToList();

            foreach (var child in children)
            {
                var childNode = new CategoryTreeNode
                {
                    Id = child.Id,
                    Name = child.Name,
                    Description = child.Description,
                    ParentId = child.ParentId,
                    NewsCount = child.NewsCount,
                    HasChildren = allCategories.Any(c => c.ParentId == child.Id),
                    Level = level,
                    IsExpanded = expandedIds.Contains(child.Id)
                };

                if (childNode.IsExpanded && level < 5) // Limit depth
                {
                    LoadChildCategories(childNode, allCategories, expandedIds, level + 1);
                }

                parent.Children.Add(childNode);
            }

            parent.Children = parent.Children.OrderBy(c => c.Name).ToList();
        }

        // Simple DTO to avoid dynamic type issues
        private class CategoryDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public int? ParentId { get; set; }
            public int NewsCount { get; set; }
        }

        private List<CategorySearchResult> SearchCategories(string term)
        {
            if (string.IsNullOrEmpty(term) || term.Length < 2)
                return new List<CategorySearchResult>();

            var categories = db.Categories
                .Where(c => c.Status && c.Name.Contains(term))
                .Take(20)
                .ToList()
                .Select(c => new CategorySearchResult
                {
                    Id = c.Id,
                    Name = c.Name,
                    Path = GetCategoryPath(c.Id),
                    NewsCount = GetDirectNewsCount(c.Id)
                })
                .OrderBy(c => c.Name)
                .ToList();

            return categories;
        }

        private void LoadNewsData(HomeViewModel model)
        {
            IQueryable<News> newsQuery = db.News.Include(n => n.Categories).Where(n => n.Status);

            // Apply filters
            if (!string.IsNullOrEmpty(model.SearchTerm))
            {
                newsQuery = newsQuery.Where(n =>
                    n.Title.Contains(model.SearchTerm) ||
                    n.Summary.Contains(model.SearchTerm) ||
                    n.Content.Contains(model.SearchTerm));
            }
            else if (model.SelectedCategoryId.HasValue)
            {
                var categoryIds = GetAllCategoryIdsInTree(model.SelectedCategoryId.Value);
                newsQuery = newsQuery.Where(n => n.Categories.Any(c => categoryIds.Contains(c.Id)));
            }

            // Apply sorting
            switch (model.SortBy)
            {
                case "oldest":
                    newsQuery = newsQuery.OrderBy(n => n.CreatedDate);
                    break;
                case "title":
                    newsQuery = newsQuery.OrderBy(n => n.Title);
                    break;
                default: // newest
                    newsQuery = newsQuery.OrderByDescending(n => n.CreatedDate);
                    break;
            }

            // Get total count
            model.TotalNewsCount = newsQuery.Count();

            // Apply pagination
            model.News = newsQuery
                .Skip((model.CurrentPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .ToList();
        }

        #region Helper Methods

        private List<int> GetAllCategoryIds()
        {
            return db.Categories.Where(c => c.Status).Select(c => c.Id).ToList();
        }

        private List<int> GetAllCategoryIdsInTree(int categoryId)
        {
            var result = new List<int> { categoryId };
            var queue = new Queue<int>();
            queue.Enqueue(categoryId);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                var childIds = db.Categories
                    .Where(c => c.ParentId == currentId && c.Status)
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

        private string GetCategoryPath(int categoryId)
        {
            try
            {
                var path = new List<string>();
                var currentId = (int?)categoryId;

                while (currentId.HasValue)
                {
                    var category = db.Categories
                        .Where(c => c.Id == currentId.Value)
                        .Select(c => new { c.Name, c.ParentId })
                        .FirstOrDefault();

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

        #region AJAX Endpoints

        [HttpPost]
        public ActionResult GetCategoryTreePartial(
            int? selectedCategoryId,
            int[] expandedIds = null,
            string searchTerm = "",
            string categorySearch = "")
        {
            var model = new HomeViewModel
            {
                SelectedCategoryId = selectedCategoryId,
                SearchTerm = searchTerm,
                CategorySearchTerm = categorySearch,
                ExpandedCategoryIds = expandedIds?.ToList() ?? new List<int>()
            };

            // Handle category search
            if (!string.IsNullOrEmpty(categorySearch))
            {
                model.CategorySearchResults = SearchCategories(categorySearch);
            }
            else
            {
                // Build category tree
                model.Categories = BuildCategoryTree(model.ExpandedCategoryIds);
            }

            return PartialView("_CategoryTree", model);
        }

        [HttpPost]
        public ActionResult GetNewsPartial(
            int? categoryId,
            string searchTerm = "",
            int page = 1,
            string sortBy = "newest",
            int pageSize = 12)
        {
            var model = new HomeViewModel
            {
                SelectedCategoryId = categoryId,
                SearchTerm = searchTerm,
                CurrentPage = page,
                PageSize = pageSize,
                SortBy = sortBy
            };

            // Get selected category
            if (categoryId.HasValue)
            {
                model.SelectedCategory = db.Categories.Find(categoryId.Value);
            }

            // Load news data
            LoadNewsData(model);

            return PartialView("_NewsList", model);
        }

        [HttpGet]
        public ActionResult GetAllCategoryIdsJson()
        {
            try
            {
                var categoryIds = db.Categories
                    .Where(c => c.Status)
                    .Select(c => c.Id)
                    .ToList();

                return Json(new { success = true, categoryIds = categoryIds }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region API Methods (Keep for backward compatibility)

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

        [HttpGet]
        public ActionResult QuickSearch(string term, int page = 1, int maxResults = 20)
        {
            try
            {
                if (string.IsNullOrEmpty(term) || term.Length < 2)
                {
                    return Json(new { success = true, results = new List<object>(), totalCount = 0 }, JsonRequestBehavior.AllowGet);
                }

                // RAW SQL cho trang chủ
                var sql = @"
            SELECT TOP (@maxResults) 
                n.Id, n.Title, n.Summary, n.CreatedDate
            FROM News n
            WHERE n.Status = 1 
            AND n.Title LIKE @term
            ORDER BY n.CreatedDate DESC";

                var results = db.Database.SqlQuery<SimpleNewsItem>(sql,
                    new System.Data.SqlClient.SqlParameter("@maxResults", maxResults),
                    new System.Data.SqlClient.SqlParameter("@term", "%" + term + "%")
                ).ToList()
                .Select(n => new
                {
                    Id = n.Id,
                    Title = n.Title ?? "",
                    Summary = n.Summary != null && n.Summary.Length > 150
                             ? n.Summary.Substring(0, 150) + "..."
                             : (n.Summary ?? ""),
                    CreatedDate = n.CreatedDate.ToString("dd/MM/yyyy"),
                    Categories = new List<string>() // Tạm thời empty cho nhanh
                })
                .ToList();

                return Json(new
                {
                    success = true,
                    results = results,
                    totalCount = results.Count,
                    currentPage = page,
                    totalPages = 1
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
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