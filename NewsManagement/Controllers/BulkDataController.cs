using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NewsManagement.Models;

namespace NewsManagement.Controllers
{
    public class BulkDataController : Controller
    {
        private TinTucEntities2 db = new TinTucEntities2();

        public ActionResult Generate()
        {
            ViewBag.Title = "Tạo dữ liệu hàng loạt";
            ViewBag.TotalCategories = db.Categories.Count(c => c.Status);
            ViewBag.TotalNews = db.News.Count();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> GenerateBulkNews(int totalNews = 40000000, int batchSize = 10000)
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var categories = db.Categories.Where(c => c.Status).Select(c => c.Id).ToList();
                if (!categories.Any())
                {
                    return Json(new { success = false, message = "Không có danh mục nào để thêm tin tức" });
                }

                var random = new Random();
                var newsGenerated = 0;
                var batches = (int)Math.Ceiling((double)totalNews / batchSize);

                var titleTemplates = new[]
                {
                    "Tin tức quan trọng về {0}",
                    "Cập nhật mới nhất: {0}",
                    "Thông tin hot: {0}",
                    "Báo cáo về {0}",
                    "Phân tích {0}",
                    "Khám phá {0}",
                    "Nghiên cứu mới về {0}",
                    "Xu hướng {0} năm 2024",
                    "Đánh giá {0}",
                    "Hướng dẫn về {0}",
                    "Tổng quan về {0}",
                    "Thực trạng {0} hiện tại",
                    "Giải pháp cho {0}",
                    "Tầm nhìn về {0}",
                    "Chiến lược {0} 2024",
                    "Đột phá trong {0}",
                    "Cơ hội từ {0}",
                    "Thách thức của {0}",
                    "Tương lai {0}",
                    "Ứng dụng {0} trong thực tiễn"
                };

                var subjects = new[]
                {
                    "công nghệ", "kinh tế", "xã hội", "giáo dục", "y tế", "thể thao", "văn hóa",
                    "du lịch", "môi trường", "khoa học", "chính trị", "pháp luật", "giao thông",
                    "nông nghiệp", "công nghiệp", "dịch vụ", "thương mại", "đầu tư", "startup",
                    "blockchain", "AI", "machine learning", "big data", "IoT", "5G", "cloud computing",
                    "fintech", "e-commerce", "digital transformation", "cybersecurity", "automation",
                    "renewable energy", "sustainable development", "climate change", "innovation",
                    "biotechnology", "nanotechnology", "quantum computing", "virtual reality",
                    "augmented reality", "robotics", "autonomous vehicles", "smart cities"
                };

                var summaryTemplates = new[]
                {
                    "Đây là thông tin tóm tắt về {0}. Nội dung này cung cấp cái nhìn tổng quan về vấn đề được đề cập.",
                    "Bài viết này trình bày những điểm chính về {0} với các thông tin cập nhật và chính xác nhất.",
                    "Chúng tôi sẽ cùng tìm hiểu về {0} và những tác động của nó đến cuộc sống hằng ngày.",
                    "Phân tích chi tiết về {0} với góc nhìn đa chiều và dữ liệu được xác thực.",
                    "Những thông tin cần thiết về {0} mà bạn không thể bỏ qua trong thời đại hiện tại.",
                    "Cập nhật mới nhất về xu hướng và phát triển của {0} trên thế giới.",
                    "Đánh giá toàn diện về tình hình {0} và những dự báo cho tương lai.",
                    "Khám phá những cơ hội và thách thức mà {0} mang lại cho doanh nghiệp."
                };

                System.Diagnostics.Debug.WriteLine($"🚀 Bắt đầu tạo {totalNews:N0} tin tức với batch size {batchSize:N0}");

                for (int batch = 0; batch < batches; batch++)
                {
                    var currentBatchSize = Math.Min(batchSize, totalNews - newsGenerated);

                    await GenerateNewsBatch(currentBatchSize, categories, titleTemplates, subjects, summaryTemplates, random);

                    newsGenerated += currentBatchSize;

                    var progress = (double)newsGenerated / totalNews * 100;
                    System.Diagnostics.Debug.WriteLine($"📊 Batch {batch + 1}/{batches} completed. Progress: {progress:F1}% ({newsGenerated:N0}/{totalNews:N0})");

                    if (batch % 10 == 0) // Mỗi 10 batch pause 100ms
                    {
                        await Task.Delay(100);
                    }
                }

                stopwatch.Stop();

                System.Diagnostics.Debug.WriteLine($"✅ Hoàn thành tạo {newsGenerated:N0} tin tức trong {stopwatch.Elapsed.TotalMinutes:F1} phút");

                return Json(new
                {
                    success = true,
                    message = $"Đã tạo thành công {newsGenerated:N0} tin tức trong {stopwatch.Elapsed.TotalMinutes:F1} phút",
                    totalGenerated = newsGenerated,
                    timeElapsed = stopwatch.Elapsed.ToString(@"hh\:mm\:ss")
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Lỗi tạo tin tức: {ex.Message}");
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        private async Task GenerateNewsBatch(int batchSize, List<int> categoryIds, string[] titleTemplates,
            string[] subjects, string[] summaryTemplates, Random random)
        {
            var connectionString = db.Database.Connection.ConnectionString;

            var newsTable = new DataTable();
            newsTable.Columns.Add("Title", typeof(string));
            newsTable.Columns.Add("Summary", typeof(string));
            newsTable.Columns.Add("Content", typeof(string));
            newsTable.Columns.Add("CreatedDate", typeof(DateTime));
            newsTable.Columns.Add("Ordering", typeof(int));
            newsTable.Columns.Add("Status", typeof(bool));

            var baseDate = DateTime.Now.AddYears(-1); // Tạo tin tức từ 2 năm trước

            // Generate news data
            for (int i = 0; i < batchSize; i++)
            {
                var subject = subjects[random.Next(subjects.Length)];
                var title = string.Format(titleTemplates[random.Next(titleTemplates.Length)], subject);
                var summary = string.Format(summaryTemplates[random.Next(summaryTemplates.Length)], subject);
                var content = GenerateRandomContent(subject, random);
                var createdDate = baseDate.AddDays(random.Next(0, 730)).AddHours(random.Next(0, 24)).AddMinutes(random.Next(0, 60));

                newsTable.Rows.Add(
                    title,
                    summary,
                    content,
                    createdDate,
                    random.Next(1, 100), // ordering
                    random.Next(0, 10) < 8 // 80% chance of being active
                );
            }

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Insert News first
                        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
                        {
                            bulkCopy.DestinationTableName = "News";
                            bulkCopy.BatchSize = batchSize;
                            bulkCopy.BulkCopyTimeout = 300;

                            // Chỉ định ánh xạ cột (bắt buộc để tránh lỗi)
                            bulkCopy.ColumnMappings.Add("Title", "Title");
                            bulkCopy.ColumnMappings.Add("Summary", "Summary");
                            bulkCopy.ColumnMappings.Add("Content", "Content");
                            bulkCopy.ColumnMappings.Add("CreatedDate", "CreatedDate");
                            bulkCopy.ColumnMappings.Add("Ordering", "Ordering");
                            bulkCopy.ColumnMappings.Add("Status", "Status");

                            await bulkCopy.WriteToServerAsync(newsTable);
                        }


                        // Get the inserted News IDs using a more reliable approach
                        var newsIds = new List<int>();
                        var getNewsIdsQuery = "SELECT TOP (@batchSize) Id FROM News ORDER BY Id DESC";

                        using (var command = new SqlCommand(getNewsIdsQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@batchSize", batchSize);
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    newsIds.Add(Convert.ToInt32(reader["Id"]));
                                }
                            }
                        }

                        // Create NewsCategory mappings using a DataTable for bulk insert
                        var newsCategoryTable = new DataTable();
                        newsCategoryTable.Columns.Add("NewsId", typeof(int));
                        newsCategoryTable.Columns.Add("CategoryId", typeof(int));

                        foreach (var newsId in newsIds)
                        {
                            // Random 1-3 categories per news
                            var categoryCount = random.Next(1, 4);
                            var selectedCategories = new HashSet<int>();

                            for (int j = 0; j < categoryCount; j++)
                            {
                                var categoryId = categoryIds[random.Next(categoryIds.Count)];
                                if (selectedCategories.Add(categoryId))
                                {
                                    newsCategoryTable.Rows.Add(newsId, categoryId);
                                }
                            }
                        }

                        // Insert NewsCategory mappings
                        if (newsCategoryTable.Rows.Count > 0)
                        {
                            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
                            {
                                bulkCopy.DestinationTableName = "NewsCategory";
                                bulkCopy.BatchSize = newsCategoryTable.Rows.Count;
                                bulkCopy.BulkCopyTimeout = 300;

                                await bulkCopy.WriteToServerAsync(newsCategoryTable);
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception($"Lỗi trong batch: {ex.Message}", ex);
                    }
                }
            }
        }
        //chawc cua toi chuwa xong
        private string GenerateRandomContent(string subject, Random random)
        {
            var contentTemplates = new[]
            {
                $"Trong thời đại phát triển mạnh mẽ của {subject}, chúng ta đang chứng kiến những thay đổi căn bản và sâu sắc. " +
                $"Các chuyên gia đánh giá rằng {subject} sẽ tiếp tục là xu hướng dẫn đầu trong thời gian tới. " +
                $"Việc nghiên cứu và ứng dụng {subject} không chỉ mang lại lợi ích kinh tế mà còn tác động tích cực đến xã hội. " +
                $"Nhiều doanh nghiệp đã và đang đầu tư mạnh mẽ vào lĩnh vực {subject} để nâng cao năng lực cạnh tranh. " +
                $"Triển vọng phát triển của {subject} trong tương lai được đánh giá là rất tích cực và bền vững. " +
                $"Các nghiên cứu gần đây cho thấy {subject} có tiềm năng lớn trong việc giải quyết các vấn đề toàn cầu. " +
                $"Sự phát triển của {subject} đã tạo ra nhiều cơ hội việc làm mới và thúc đẩy tăng trưởng kinh tế. " +
                $"Các chính phủ trên thế giới đều đang ưu tiên đầu tư vào {subject} để không bị tụt hậu. " +
                $"Giáo dục và đào tạo về {subject} đang trở thành yêu cầu cấp thiết của thị trường lao động. " +
                $"Việc ứng dụng {subject} trong đời sống hàng ngày đang mang lại nhiều tiện ích và cải thiện chất lượng cuộc sống.",

                $"Báo cáo mới nhất cho thấy {subject} đang thu hút sự quan tâm đặc biệt từ cộng đồng quốc tế. " +
                $"Các nghiên cứu về {subject} đã đạt được nhiều thành tựu đáng kể và mở ra những cơ hội mới. " +
                $"Sự phát triển của {subject} không chỉ ảnh hưởng đến một lĩnh vực mà còn tác động đa ngành. " +
                $"Chính phủ đã ban hành nhiều chính sách hỗ trợ để thúc đẩy việc phát triển {subject}. " +
                $"Dự báo cho thấy {subject} sẽ tiếp tục tăng trưởng mạnh mẽ trong những năm tới. " +
                $"Các tập đoàn lớn đều đang đẩy mạnh đầu tư vào {subject} để tạo lợi thế cạnh tranh. " +
                $"Startup trong lĩnh vực {subject} đang nhận được sự quan tâm lớn từ các nhà đầu tư. " +
                $"Hợp tác quốc tế trong phát triển {subject} đang được đẩy mạnh trên toàn cầu. " +
                $"Các tiêu chuẩn và quy định về {subject} đang được xây dựng và hoàn thiện. " +
                $"Tương lai của {subject} hứa hẹn sẽ mang lại những đột phá quan trọng cho nhân loại.",

                $"Cuộc cách mạng {subject} đang diễn ra với tốc độ chóng mặt và mang lại nhiều cơ hội mới. " +
                $"Việc ứng dụng {subject} vào thực tiễn đã mang lại những kết quả tích cực vượt mong đợi. " +
                $"Các chuyên gia khuyến nghị cần có sự chuẩn bị kỹ lưỡng để tận dụng tối đa tiềm năng của {subject}. " +
                $"Đầu tư vào {subject} được xem là chiến lược dài hạn để phát triển bền vững. " +
                $"Ecosystm {subject} đang phát triển mạnh mẽ với sự tham gia của nhiều bên liên quan. " +
                $"Các khóa học và chương trình đào tạo về {subject} đang được mở rộng tại các trường đại học. " +
                $"Sự hợp tác giữa học viện và doanh nghiệp trong {subject} đang mang lại nhiều thành quả. " +
                $"Các sự kiện và hội thảo về {subject} đang thu hút đông đảo chuyên gia tham gia. " +
                $"Media và truyền thông đóng vai trò quan trọng trong việc phổ biến kiến thức về {subject}. " +
                $"Tương lai của {subject} được định hướng bởi các yếu tố công nghệ và xã hội."
            };

            return contentTemplates[random.Next(contentTemplates.Length)];
        }

        [HttpGet]
        public JsonResult GetDatabaseStats()
        {
            try
            {
                var stats = new
                {
                    totalCategories = db.Categories.Count(),
                    activeCategories = db.Categories.Count(c => c.Status),
                    totalNews = db.News.Count(),
                    activeNews = db.News.Count(n => n.Status)
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