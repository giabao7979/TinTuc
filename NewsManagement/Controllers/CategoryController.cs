using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using PagedList;
using NewsManagement.Models;
using System.Threading.Tasks;

namespace NewsManagement.Controllers
{
    public class CategoryController : Controller
    {
        private TinTucEntities2 db = new TinTucEntities2();

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
                //NewsCount = GetDirectNewsCount(c.Id),
                //TotalNewsCount = GetTotalNewsCountInCategoryTree(c.Id),
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

            // SỬ DỤNG PHƯƠNG PHÁP TỐI ƯU - LOAD TẤT CẢ CATEGORIES MỘT LẦN
            var availableParents = GetAvailableParentsOptimized(id);
            ViewBag.ParentId = new SelectList(availableParents, "Id", "DisplayName", category.ParentId);

            return View(category);
        }

        // ===== THÊM METHODS TỐI ƯU MỚI =====

        /// <summary>
        /// Tối ưu hóa việc lấy danh sách danh mục cha có thể chọn
        /// Chỉ 1 lần truy cập database thay vì N lần
        /// </summary>
        private List<CategoryDropdownItem> GetAvailableParentsOptimized(int editingCategoryId)
        {
            // 1. Load TẤT CẢ categories một lần duy nhất
            var allCategories = db.Categories
                .Where(c => c.Status && c.Id != editingCategoryId) // Loại trừ chính nó
                .Select(c => new SimpleCategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    ParentId = c.ParentId
                })
                .ToList();

            // 2. Tạo dictionary để lookup nhanh
            var categoryDict = allCategories.ToDictionary(c => c.Id);

            // 3. Tìm tất cả descendants của category đang edit (không cho chọn)
            var descendantIds = GetAllDescendantsOptimized(editingCategoryId, allCategories);

            // 4. Lọc ra những categories có thể chọn làm parent
            var availableCategories = allCategories
                .Where(c => !descendantIds.Contains(c.Id)) // Không phải descendant
                .ToList();

            // 5. Tạo display names với hierarchy path
            var result = new List<CategoryDropdownItem>();
            foreach (var cat in availableCategories)
            {
                var path = BuildCategoryPathOptimized(cat.Id, categoryDict);
                result.Add(new CategoryDropdownItem
                {
                    Id = cat.Id,
                    Name = cat.Name,
                    DisplayName = path
                });
            }

            return result.OrderBy(c => c.DisplayName).ToList();
        }

        /// <summary>
        /// Tìm tất cả descendants của một category mà KHÔNG cần truy cập database nhiều lần
        /// </summary>
        private HashSet<int> GetAllDescendantsOptimized(int categoryId, List<SimpleCategoryDto> allCategories)
        {
            var descendants = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(categoryId);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();

                // Tìm tất cả children của currentId trong memory
                var children = allCategories.Where(c => c.ParentId == currentId);

                foreach (var child in children)
                {
                    if (!descendants.Contains(child.Id))
                    {
                        descendants.Add(child.Id);
                        queue.Enqueue(child.Id); // Tiếp tục tìm descendants của child
                    }
                }
            }

            return descendants;
        }

        /// <summary>
        /// Xây dựng đường dẫn category path mà không cần truy cập database
        /// </summary>
        private string BuildCategoryPathOptimized(int categoryId, Dictionary<int, SimpleCategoryDto> categoryDict)
        {
            var path = new List<string>();
            var currentId = (int?)categoryId;
            var visited = new HashSet<int>(); // Tránh infinite loop

            while (currentId.HasValue && !visited.Contains(currentId.Value))
            {
                visited.Add(currentId.Value);

                if (categoryDict.TryGetValue(currentId.Value, out var category))
                {
                    path.Insert(0, category.Name ?? "");
                    currentId = category.ParentId;
                }
                else
                {
                    break;
                }

                // Bảo vệ tránh infinite loop
                if (path.Count > 10) break;
            }

            return string.Join(" > ", path);
        }

        // ===== DTO CLASS CHO TỐI ƯU =====
        public class SimpleCategoryDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int? ParentId { get; set; }
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
        [HttpGet]
        public async Task<JsonResult> GetAvailableCategoriesForEdit(int editingCategoryId, string searchTerm = "", int page = 1, int pageSize = 50)
        {
            try
            {
                // Sử dụng CTE để tìm tất cả descendants của category đang edit
                var sql = @"
            WITH CategoryHierarchy AS (
                -- Bắt đầu từ category đang edit
                SELECT Id, ParentId, 0 as Level
                FROM Category 
                WHERE Id = @editingCategoryId
                
                UNION ALL
                
                -- Recursively tìm tất cả children/descendants
                SELECT c.Id, c.ParentId, ch.Level + 1
                FROM Category c
                INNER JOIN CategoryHierarchy ch ON c.ParentId = ch.Id
                WHERE ch.Level < 20  -- Giới hạn độ sâu để tránh infinite loop
            ),
            AvailableCategories AS (
                SELECT c.Id, c.Name, c.ParentId, c.Ordering,
                       ROW_NUMBER() OVER (ORDER BY 
                           CASE WHEN @searchTerm = '' THEN c.Ordering ELSE 0 END,
                           CASE WHEN @searchTerm != '' AND c.Name LIKE @searchTerm + '%' THEN 0 ELSE 1 END,
                           c.Name
                       ) as RowNum
                FROM Category c
                WHERE c.Status = 1 
                  AND c.Id != @editingCategoryId  -- Loại trừ chính category đang edit
                  AND c.Id NOT IN (SELECT Id FROM CategoryHierarchy) -- Loại trừ tất cả descendants
                  AND (@searchTerm = '' OR c.Name LIKE '%' + @searchTerm + '%')
            )
            SELECT ac.Id, ac.Name, ac.ParentId, ac.Ordering,
                   (SELECT COUNT(*) 
                    FROM News n 
                    INNER JOIN NewsCategory nc ON n.Id = nc.NewsId 
                    WHERE nc.CategoryId = ac.Id AND n.Status = 1) as NewsCount,
                   CASE WHEN EXISTS(
                       SELECT 1 FROM Category child 
                       WHERE child.ParentId = ac.Id AND child.Status = 1
                   ) THEN 1 ELSE 0 END as HasChildren
            FROM AvailableCategories ac
            WHERE ac.RowNum BETWEEN @startRow AND @endRow
            ORDER BY ac.RowNum";

                var startRow = (page - 1) * pageSize + 1;
                var endRow = page * pageSize;

                var results = db.Database.SqlQuery<CategoryForEditResult>(sql,
                    new System.Data.SqlClient.SqlParameter("@editingCategoryId", editingCategoryId),
                    new System.Data.SqlClient.SqlParameter("@searchTerm", searchTerm ?? ""),
                    new System.Data.SqlClient.SqlParameter("@startRow", startRow),
                    new System.Data.SqlClient.SqlParameter("@endRow", endRow)
                ).ToList();

                // Đếm tổng số categories available
                var countSql = @"
            WITH CategoryHierarchy AS (
                SELECT Id, ParentId, 0 as Level
                FROM Category WHERE Id = @editingCategoryId
                UNION ALL
                SELECT c.Id, c.ParentId, ch.Level + 1
                FROM Category c
                INNER JOIN CategoryHierarchy ch ON c.ParentId = ch.Id
                WHERE ch.Level < 20
            )
            SELECT COUNT(*)
            FROM Category c
            WHERE c.Status = 1 
              AND c.Id != @editingCategoryId
              AND c.Id NOT IN (SELECT Id FROM CategoryHierarchy)
              AND (@searchTerm = '' OR c.Name LIKE '%' + @searchTerm + '%')";

                var totalCount = db.Database.SqlQuery<int>(countSql,
                    new System.Data.SqlClient.SqlParameter("@editingCategoryId", editingCategoryId),
                    new System.Data.SqlClient.SqlParameter("@searchTerm", searchTerm ?? "")
                ).FirstOrDefault();

                // Build response với category paths
                var categories = results.Select(r => new
                {
                    Id = r.Id,
                    Name = r.Name,
                    ParentId = r.ParentId,
                    NewsCount = r.NewsCount,
                    HasChildren = r.HasChildren > 0,
                    Path = GetCategoryPathFast(r.Id) // Sử dụng cache nếu có
                }).ToList();

                return Json(new
                {
                    success = true,
                    categories = categories,
                    totalCount = totalCount,
                    currentPage = page,
                    pageSize = pageSize,
                    hasMore = (page * pageSize) < totalCount,
                    searchTerm = searchTerm
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message,
                    details = ex.InnerException?.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet]
        public JsonResult GetCategoryName(int id)
        {
            try
            {
                var category = db.Categories
                    .Where(c => c.Id == id)
                    .Select(c => new { c.Name, c.ParentId })
                    .FirstOrDefault();

                if (category != null)
                {
                    return Json(new
                    {
                        success = true,
                        name = category.Name,
                        parentId = category.ParentId
                    }, JsonRequestBehavior.AllowGet);
                }

                return Json(new { success = false, message = "Category not found" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet]
        public JsonResult ValidateCategorySelection(int editingCategoryId, int? parentId)
        {
            try
            {
                if (!parentId.HasValue)
                {
                    return Json(new { success = true, valid = true }, JsonRequestBehavior.AllowGet);
                }

                // Check if trying to select self
                if (parentId.Value == editingCategoryId)
                {
                    return Json(new
                    {
                        success = true,
                        valid = false,
                        message = "Không thể chọn chính danh mục này làm danh mục cha"
                    }, JsonRequestBehavior.AllowGet);
                }

                // Check if parent is a descendant
                var isDescendant = IsDescendantOfOptimized(parentId.Value, editingCategoryId);
                if (isDescendant)
                {
                    return Json(new
                    {
                        success = true,
                        valid = false,
                        message = "Không thể chọn danh mục con làm danh mục cha"
                    }, JsonRequestBehavior.AllowGet);
                }

                return Json(new { success = true, valid = true }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        private string GetCategoryPathFast(int categoryId)
        {
            // Sử dụng cache để tránh rebuild path nhiều lần
            var cacheKey = $"CategoryPath_{categoryId}";

            // Nếu có cache system (Redis/MemoryCache), check cache trước
            // if (Cache[cacheKey] != null) return Cache[cacheKey].ToString();

            try
            {
                var pathSql = @"
            WITH CategoryPath AS (
                SELECT Id, Name, ParentId, CAST(Name as NVARCHAR(1000)) as Path, 0 as Level
                FROM Category 
                WHERE Id = @categoryId
                
                UNION ALL
                
                SELECT c.Id, c.Name, c.ParentId, 
                       CAST(c.Name + ' > ' + cp.Path as NVARCHAR(1000)) as Path, 
                       cp.Level + 1
                FROM Category c
                INNER JOIN CategoryPath cp ON c.Id = cp.ParentId
                WHERE cp.Level < 10
            )
            SELECT TOP 1 Path 
            FROM CategoryPath 
            ORDER BY Level DESC";

                var path = db.Database.SqlQuery<string>(pathSql,
                    new System.Data.SqlClient.SqlParameter("@categoryId", categoryId)
                ).FirstOrDefault();

                // Cache result
                // Cache.Insert(cacheKey, path ?? "Unknown", DateTime.Now.AddMinutes(30));

                return path ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
        private bool IsDescendantOfOptimized(int childId, int ancestorId)
        {
            try
            {
                var sql = @"
            WITH CategoryHierarchy AS (
                SELECT Id, ParentId, 0 as Level
                FROM Category 
                WHERE Id = @childId
                
                UNION ALL
                
                SELECT c.Id, c.ParentId, ch.Level + 1
                FROM Category c
                INNER JOIN CategoryHierarchy ch ON c.Id = ch.ParentId
                WHERE ch.Level < 20
            )
            SELECT COUNT(*)
            FROM CategoryHierarchy
            WHERE Id = @ancestorId";

                var count = db.Database.SqlQuery<int>(sql,
                    new System.Data.SqlClient.SqlParameter("@childId", childId),
                    new System.Data.SqlClient.SqlParameter("@ancestorId", ancestorId)
                ).FirstOrDefault();

                return count > 0;
            }
            catch
            {
                return false;
            }
        }
        public class CategoryForEditResult
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int? ParentId { get; set; }
            public int Ordering { get; set; }
            public int NewsCount { get; set; }
            public int HasChildren { get; set; }
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

        // THAY THẾ method SearchCategories trong CategoryController.cs
        [HttpGet]
        public JsonResult SearchCategories(string term, int page = 1, int pageSize = 20)
        {
            try
            {
                if (string.IsNullOrEmpty(term) || term.Length < 2)
                {
                    return Json(new { success = true, categories = new List<object>(), totalCount = 0 }, JsonRequestBehavior.AllowGet);
                }

                // Sử dụng stored procedure
                var results = db.Database.SqlQuery<CategorySearchResult>(
                    "EXEC sp_SearchCategories @SearchTerm, @PageSize, @Page",
                    new System.Data.SqlClient.SqlParameter("@SearchTerm", term),
                    new System.Data.SqlClient.SqlParameter("@PageSize", pageSize),
                    new System.Data.SqlClient.SqlParameter("@Page", page)
                ).ToList();

                var categories = results.Select(c => new
                {
                    Id = c.Id,
                    Name = c.Name,
                    NewsCount = c.NewsCount,
                    Path = c.FullPath,
                    HasChildren = c.ChildCount > 0
                }).ToList();

                return Json(new
                {
                    success = true,
                    categories = categories,
                    totalCount = categories.Count,
                    currentPage = page,
                    hasMore = categories.Count == pageSize
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // DTO class cho search results
        public class CategorySearchResult
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int? ParentId { get; set; }
            public string FullPath { get; set; }
            public int NewsCount { get; set; }
            public int ChildCount { get; set; }
        }

        // THÊM method mới vào CategoryController
        [HttpGet]
        public JsonResult GetRootCategoriesPaged(int page = 1, int pageSize = 50)
        {
            try
            {
                var results = db.Database.SqlQuery<CategorySearchResult>(
                    "EXEC sp_GetRootCategories @PageSize, @Page",
                    new System.Data.SqlClient.SqlParameter("@PageSize", pageSize),
                    new System.Data.SqlClient.SqlParameter("@Page", page)
                ).ToList();

                var categories = results.Select(c => new
                {
                    Id = c.Id,
                    Name = c.Name,
                    NewsCount = c.NewsCount,
                    HasChildren = c.ChildCount > 0
                }).ToList();

                return Json(new
                {
                    success = true,
                    categories = categories,
                    currentPage = page,
                    hasMore = categories.Count == pageSize
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // THAY THẾ GetSubcategories method
        [HttpGet]
        public ActionResult GetSubcategories(int parentId)
        {
            try
            {
                var results = db.Database.SqlQuery<CategorySearchResult>(
                    "EXEC sp_GetSubcategories @ParentId, @PageSize",
                    new System.Data.SqlClient.SqlParameter("@ParentId", parentId),
                    new System.Data.SqlClient.SqlParameter("@PageSize", 100) // Limit subcategories
                ).ToList();

                var subcategories = results.Select(c => new
                {
                    Id = c.Id,
                    Name = c.Name,
                    NewsCount = c.NewsCount,
                    HasChildren = c.ChildCount > 0
                }).ToList();

                return Json(new { success = true, subcategories = subcategories }, JsonRequestBehavior.AllowGet);
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
