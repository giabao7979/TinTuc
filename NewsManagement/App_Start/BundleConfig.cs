using System.Web;
using System.Web.Optimization;

namespace NewsManagement
{
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/bootstrap.min.css",
                      "~/Content/all.min.css",
                      "~/Content/site.css",
                      "~/Content/home-page.css",
                      "~/Content/layout.css",
                      "~/Content/creat.css",
                      "~/Content/item.css"));

            bundles.Add(new ScriptBundle("~/bundles/main").Include(
                      "~/Scripts/jquery-{version}.js",
                      "~/Scripts/bootstrap.bundle.min.js",
                      "~/Scripts/home-page.js"
            ));

            BundleTable.EnableOptimizations = true; // Bắt buộc để minify + gộp
        }
    }

}
