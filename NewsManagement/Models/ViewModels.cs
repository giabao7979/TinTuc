using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NewsManagement.Models;

namespace NewsManagement.Models
{
    // ===== FILTER VIEW MODEL =====
    public class NewsFilterViewModel
    {
        public string SearchTerm { get; set; }
        public int? CategoryId { get; set; }
        public bool? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string DateRange { get; set; }
        public string SortBy { get; set; } = "date_desc";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    // ===== LIST VIEW MODEL =====
    public class NewsListViewModel
    {
        public List<News> News { get; set; } = new List<News>();
        public NewsFilterViewModel Filter { get; set; } = new NewsFilterViewModel();

        // Pagination properties
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }

        // Computed properties
        public bool HasNews => News != null && News.Count > 0;
        public bool IsFiltered => !string.IsNullOrEmpty(Filter?.SearchTerm) ||
                                  Filter?.CategoryId.HasValue == true ||
                                  Filter?.Status.HasValue == true ||
                                  Filter?.DateFrom.HasValue == true ||
                                  Filter?.DateTo.HasValue == true ||
                                  !string.IsNullOrEmpty(Filter?.DateRange);
    }

    // ===== CREATE/EDIT VIEW MODEL =====
    public class NewsCreateEditViewModel
    {
        [Required]
        public News News { get; set; } = new News();

        [Required(ErrorMessage = "Vui lòng chọn ít nhất một danh mục")]
        public List<int> SelectedCategoryIds { get; set; } = new List<int>();

        public List<CategoryTreeViewModel> AvailableCategories { get; set; } = new List<CategoryTreeViewModel>();
        public List<Category> PopularCategories { get; set; } = new List<Category>();

        // Helper properties
        public bool IsEdit => News?.Id > 0;
        public string FormTitle => IsEdit ? "Chỉnh sửa tin tức" : "Thêm tin tức mới";
        public string SubmitButtonText => IsEdit ? "Cập nhật" : "Tạo tin tức";
    }

    // ===== DETAIL VIEW MODEL =====
    public class NewsDetailViewModel
    {
        public News News { get; set; }
        public List<News> RelatedNews { get; set; } = new List<News>();
        public string CategoryPath { get; set; }
        public int ViewCount { get; set; }
        public DateTime LastModified { get; set; }

        // Computed properties
        public bool HasRelatedNews => RelatedNews != null && RelatedNews.Count > 0;
        public string ShareUrl => $"/News/Details/{News?.Id}";
        public string FormattedCreatedDate => News?.CreatedDate.ToString("dd/MM/yyyy HH:mm");
        public string TimeAgo
        {
            get
            {
                if (News == null) return "";

                var timeSpan = DateTime.Now - News.CreatedDate;
                if (timeSpan.Days > 0)
                    return $"{timeSpan.Days} ngày trước";
                else if (timeSpan.Hours > 0)
                    return $"{timeSpan.Hours} giờ trước";
                else if (timeSpan.Minutes > 0)
                    return $"{timeSpan.Minutes} phút trước";
                else
                    return "Vừa xong";
            }
        }
    }

    // ===== DELETE VIEW MODEL =====
    public class NewsDeleteViewModel
    {
        public News News { get; set; }
        public int RelatedNewsCount { get; set; }
        public List<string> CategoryNames { get; set; } = new List<string>();

        // Computed properties
        public string CategoriesText => string.Join(", ", CategoryNames);
        public bool HasRelatedNews => RelatedNewsCount > 0;
    }

    // ===== CATEGORY TREE VIEW MODEL =====
    public class CategoryTreeViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public List<CategoryTreeViewModel> Children { get; set; } = new List<CategoryTreeViewModel>();

        // Helper properties
        public bool HasChildren => Children != null && Children.Count > 0;
        public string IndentedName => new string('-', Level * 2) + " " + Name;
        public string CssClass => $"category-level-{Level}";
    }

    // ===== BULK ACTION VIEW MODEL =====
    public class BulkActionViewModel
    {
        public string Action { get; set; }
        public int[] SelectedIds { get; set; }
        public string ConfirmationMessage { get; set; }
    }

    // ===== STATISTICS VIEW MODEL =====
    public class NewsStatisticsViewModel
    {
        public int TotalNews { get; set; }
        public int ActiveNews { get; set; }
        public int InactiveNews { get; set; }
        public int NewsThisMonth { get; set; }
        public int NewsThisWeek { get; set; }
        public int NewsToday { get; set; }

        // Top categories
        public List<CategoryStatsViewModel> TopCategories { get; set; } = new List<CategoryStatsViewModel>();

        // Recent activities
        public List<News> RecentNews { get; set; } = new List<News>();

        // Computed properties
        public double ActivePercentage => TotalNews > 0 ? (double)ActiveNews / TotalNews * 100 : 0;
        public double InactivePercentage => TotalNews > 0 ? (double)InactiveNews / TotalNews * 100 : 0;
    }

    public class CategoryStatsViewModel
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public int NewsCount { get; set; }
        public double Percentage { get; set; }
    }

    // ===== SEARCH RESULT VIEW MODEL =====
    public class NewsSearchResultViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool Status { get; set; }
        public List<string> Categories { get; set; } = new List<string>();

        // Computed properties
        public string FormattedDate => CreatedDate.ToString("dd/MM/yyyy");
        public string StatusText => Status ? "Hoạt động" : "Tạm dừng";
        public string StatusClass => Status ? "success" : "secondary";
        public string CategoriesText => string.Join(", ", Categories);
    }

    // ===== QUICK ACTIONS VIEW MODEL =====
    public class NewsQuickActionsViewModel
    {
        public int NewsId { get; set; }
        public string Title { get; set; }
        public bool CurrentStatus { get; set; }
        public List<QuickActionItem> AvailableActions { get; set; } = new List<QuickActionItem>();
    }

    public class QuickActionItem
    {
        public string Action { get; set; }
        public string Text { get; set; }
        public string Icon { get; set; }
        public string CssClass { get; set; }
        public bool RequireConfirmation { get; set; }
    }

    // ===== PAGINATION VIEW MODEL =====
    public class PaginationViewModel
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }
        public string BaseUrl { get; set; }
        public Dictionary<string, string> RouteValues { get; set; } = new Dictionary<string, string>();

        // Computed properties
        public int StartIndex => (CurrentPage - 1) * PageSize + 1;
        public int EndIndex => Math.Min(CurrentPage * PageSize, TotalCount);
        public List<int> PageNumbers
        {
            get
            {
                var pages = new List<int>();
                var start = Math.Max(1, CurrentPage - 2);
                var end = Math.Min(TotalPages, CurrentPage + 2);

                for (int i = start; i <= end; i++)
                {
                    pages.Add(i);
                }

                return pages;
            }
        }
    }
}