using System;
using System.ComponentModel.DataAnnotations;

namespace NewsManagement.Models
{
    public class NewsListItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool Status { get; set; }
        public string CategoryNames { get; set; }
        public int Ordering { get; set; }
    }

    public class SimpleNewsItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CategoryTreeItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? ParentId { get; set; }
        public int NewsCount { get; set; }
        public bool HasChildren { get; set; }
    }
}