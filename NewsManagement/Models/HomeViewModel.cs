using System;
using System.Collections.Generic;
using System.Linq;
using NewsManagement.Models;

namespace NewsManagement.Models
{
    public class HomeViewModel
    {
        public HomeViewModel()
        {
            Categories = new List<CategoryTreeNode>();
            News = new List<News>();
            ExpandedCategoryIds = new List<int>();
            CategorySearchResults = new List<CategorySearchResult>();
            CurrentPage = 1;
            PageSize = 12;
            SortBy = "newest";
        }

        // Category Tree
        public List<CategoryTreeNode> Categories { get; set; }
        public List<int> ExpandedCategoryIds { get; set; }

        // Selected Category
        public int? SelectedCategoryId { get; set; }
        public Category SelectedCategory { get; set; }

        // News Data
        public List<News> News { get; set; }
        public int TotalNewsCount { get; set; }

        // Pagination
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalNewsCount / PageSize);

        // Search
        public string SearchTerm { get; set; }
        public string CategorySearchTerm { get; set; }
        public List<CategorySearchResult> CategorySearchResults { get; set; }

        // Sorting
        public string SortBy { get; set; }
    }

    public class CategoryTreeNode
    {
        public CategoryTreeNode()
        {
            Children = new List<CategoryTreeNode>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int? ParentId { get; set; }
        public int NewsCount { get; set; }
        public bool HasChildren { get; set; }
        public List<CategoryTreeNode> Children { get; set; }
        public int Level { get; set; }
        public bool IsExpanded { get; set; }
    }

    public class CategorySearchResult
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public int NewsCount { get; set; }
    }
}