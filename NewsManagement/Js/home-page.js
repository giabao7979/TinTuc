// ===== FIXED HOME PAGE JAVASCRIPT - Sửa lỗi jQuery undefined =====

// Global variables
var currentMode = 'category';
var currentCategoryId = 1; // Default to category 1
var currentCategoryName = '';
var searchTimeout;

// Kiểm tra jQuery đã load chưa
if (typeof jQuery === 'undefined') {
    console.error('jQuery not loaded!');
}

// Initialize page khi document ready
$(document).ready(function () {
    console.log('🚀 Page loaded, initializing with category 1...');
    console.log('jQuery version:', $.fn.jquery);

    // Kiểm tra các element cần thiết có tồn tại không
    if ($('#categories-menu').length === 0) {
        console.error('Element #categories-menu not found!');
        return;
    }

    if ($('#content-container').length === 0) {
        console.error('Element #content-container not found!');
        return;
    }

    // Load categories first
    loadCategories();

    // Wait a bit then auto-load category 1 news
    setTimeout(function () {
        console.log('⏰ Auto-loading category 1 news...');
        loadCategoryNews(1);
    }, 1000);

    // Search input events with error handling
    $('#search-input').on('keyup', function (e) {
        try {
            if (e.key === 'Enter') {
                performSearch();
                return;
            }

            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(function () {
                var query = $('#search-input').val().trim();
                if (query.length >= 2) {
                    performSearch();
                } else if (query.length === 0) {
                    clearSearch();
                }
            }, 500);
        } catch (error) {
            console.error('Error in search input handler:', error);
        }
    });

    console.log('✅ Page initialization completed');
});

// ===== MAIN FUNCTION: Load news by category ID =====
function loadCategoryNews(categoryId, includeSubcategories = true) {
    console.log('📰 loadCategoryNews called with:', categoryId, 'includeSubcategories:', includeSubcategories);

    if (!categoryId) {
        console.error('❌ Missing categoryId');
        return;
    }

    if (typeof $ === 'undefined') {
        console.error('❌ jQuery not available');
        return;
    }

    var categoryName = findCategoryNameById(categoryId);
    if (!categoryName) {
        categoryName = 'Danh mục ' + categoryId;
    }

    console.log('📁 Loading news for category:', categoryId, '-', categoryName);

    currentCategoryId = categoryId;
    currentCategoryName = categoryName;
    currentMode = 'category';

    setActiveCategory(categoryId);

    var modeText = includeSubcategories ? '(bao gồm danh mục con)' : '(chỉ danh mục này)';
    updateHeader('📁 ' + categoryName + ' ' + modeText);
    updateSubtitle('Đang tải tin tức...');
    showCategoryLoading(categoryName);

    addCategoryLoadingState(categoryId);

    $.ajax({
        url: '/Home/GetNewsByCategory',
        type: 'GET',
        data: {
            categoryId: categoryId,
            page: 1,
            pageSize: 20,
            includeSubcategories: includeSubcategories
        },
        dataType: 'json',
        timeout: 15000,
        success: function (data) {
            console.log('✅ Category news loaded:', data);

            removeCategoryLoadingState(categoryId);

            if (data.success) {
                if (data.data && data.data.length > 0) {
                    console.log('📊 Successfully loaded ' + data.data.length + ' news items');

                    var modeText = data.includeSubcategories ? '(bao gồm danh mục con)' : '(chỉ danh mục này)';
                    updateHeader('📁 ' + categoryName + ' ' + modeText);
                    updateSubtitle('Tìm thấy ' + data.totalCount + ' tin tức');

                    displayCategoryNews(data.data, categoryName, data.totalCount,
                        data.includeSubcategories, data.subcategoryCount);

                    var successMsg = 'Đã tải ' + data.data.length + ' tin tức từ "' + categoryName + '"';
                    if (data.subcategoryCount > 0) {
                        successMsg += ' và ' + data.subcategoryCount + ' danh mục con';
                    }
                    showSuccessMessage(successMsg);
                } else {
                    console.log('ℹ️ No news found for this category');
                    updateHeader('📁 ' + categoryName + ' ' + modeText);
                    updateSubtitle('Danh mục này chưa có tin tức');
                    showNoCategoryNews(categoryName);
                }
            } else {
                console.error('❌ API returned error:', data.message);
                updateHeader('❌ Lỗi tải danh mục: ' + categoryName);
                updateSubtitle('Có lỗi xảy ra khi tải dữ liệu');
                showCategoryError(categoryName + ' (API Error: ' + (data.message || 'Unknown') + ')');
            }
        },
        error: function (xhr, status, error) {
            console.error('❌ AJAX Error:', {
                status: status,
                error: error,
                responseText: xhr.responseText,
                statusCode: xhr.status
            });

            removeCategoryLoadingState(categoryId);

            var errorMsg = 'Lỗi kết nối';
            if (xhr.status === 404) {
                errorMsg = 'API không tìm thấy';
            } else if (xhr.status === 500) {
                errorMsg = 'Lỗi server';
            } else if (status === 'timeout') {
                errorMsg = 'Hết thời gian chờ';
            }

            updateHeader('❌ Lỗi tải danh mục: ' + categoryName);
            updateSubtitle('Không thể kết nối đến server');
            showCategoryError(categoryName + ' (' + errorMsg + ')');

            showErrorMessage('Không thể tải tin tức từ "' + categoryName + '"');
        }
    });
}
console.log('✅ Enhanced category system with total news count (including subcategories) loaded');
function setupSearch() {
    $('#search-input').off('keyup').on('keyup', function (e) {
        try {
            // Nếu nhấn Enter thì tìm ngay
            if (e.key === 'Enter') {
                performSearch();
                return;
            }

            // Clear timeout cũ
            clearTimeout(searchTimeout);

            var query = $(this).val().trim();

            // Nếu xóa hết thì quay về danh mục hiện tại
            if (query.length === 0) {
                clearSearch();
                return;
            }

            // Chỉ tìm khi dừng nhập (tăng thời gian chờ)
            searchTimeout = setTimeout(function () {
                if (query.length >= 3) { // Tăng từ 2 lên 3 ký tự
                    performSearch();
                }
            }, 1200); // Tăng từ 500ms lên 1200ms (1.2 giây)

        } catch (error) {
            console.error('Search error:', error);
        }
    });
}
// ===== HELPER: Find category name by ID =====
function findCategoryNameById(categoryId) {
    var categoryName = '';

    try {
        // Search in loaded category links
        $('[data-category-id="' + categoryId + '"]').each(function () {
            var name = $(this).attr('data-category-name');
            if (name) {
                categoryName = name;
                return false; // Break loop
            }
        });
    } catch (error) {
        console.error('Error in findCategoryNameById:', error);
    }

    return categoryName;
}

// ===== COMPATIBILITY: Keep old function name for existing event listeners =====
function loadNewsByCategory(categoryId, categoryName) {
    // If categoryName is provided, update the global variable
    if (categoryName) {
        currentCategoryName = categoryName;
    }

    // Call the main function
    loadCategoryNews(categoryId);
}

// ===== DISPLAY FUNCTIONS =====
function displayCategoryNews(newsArray, categoryName, totalCount, includeSubcategories, subcategoryCount) {
    console.log('✅ displayCategoryNews called with:', {
        newsCount: newsArray ? newsArray.length : 0,
        categoryName: categoryName,
        totalCount: totalCount,
        includeSubcategories: includeSubcategories,
        subcategoryCount: subcategoryCount
    });

    try {
        var html = '';

        // ✅ Header thông tin danh mục với thông tin chi tiết hơn
        html += '<div class="alert alert-primary border-left-primary mb-4">';
        html += '<div class="d-flex align-items-center">';
        html += '<i class="fas fa-folder-open mr-3" style="font-size: 1.5rem;"></i>';
        html += '<div class="flex-grow-1">';
        html += '<h5 class="mb-1"><strong>📁 ' + escapeHtml(categoryName) + '</strong></h5>';

        if (includeSubcategories && subcategoryCount > 0) {
            html += '<small class="text-muted">';
            html += '📊 Tổng <strong>' + totalCount + '</strong> tin tức từ danh mục này và <strong>' + subcategoryCount + '</strong> danh mục con';
            html += '</small>';
        } else {
            html += '<small class="text-muted">';
            html += '📊 Tổng <strong>' + totalCount + '</strong> tin tức (bao gồm cả danh mục con)';
            html += '</small>';
        }
        html += '</div>';

        // Toggle button để chuyển đổi chế độ xem
        html += '<div class="btn-group btn-group-sm ml-3" role="group">';
        html += '<button class="btn btn-outline-primary" onclick="toggleNewsMode(' + currentCategoryId + ', true)" ';
        html += 'title="Xem tin từ tất cả danh mục con">';
        html += '<i class="fas fa-sitemap mr-1"></i>Bao gồm con';
        html += '</button>';
        html += '<button class="btn btn-outline-secondary" onclick="toggleNewsMode(' + currentCategoryId + ', false)" ';
        html += 'title="Chỉ xem tin trực tiếp trong danh mục này">';
        html += '<i class="fas fa-folder mr-1"></i>Chỉ danh mục này';
        html += '</button>';
        html += '</div>';

        html += '</div>';
        html += '</div>';

        // Hiển thị tin tức
        if (newsArray && newsArray.length > 0) {
            html += '<div class="row">';

            for (var i = 0; i < newsArray.length; i++) {
                var news = newsArray[i];
                html += generateNewsCard(news, true); // Hiển thị danh mục của mỗi tin
            }

            html += '</div>';

            // Thông tin phân trang
            if (totalCount > newsArray.length) {
                html += '<div class="alert alert-info mt-3">';
                html += '<div class="d-flex align-items-center justify-content-between">';
                html += '<div>';
                html += '<i class="fas fa-info-circle mr-2"></i>';
                html += 'Hiển thị <strong>' + newsArray.length + '</strong> trong tổng số <strong>' + totalCount + '</strong> tin tức';
                html += '</div>';
                html += '<button class="btn btn-outline-primary" onclick="loadMoreNews(' + currentCategoryId + ')">';
                html += '<i class="fas fa-plus mr-1"></i>Tải thêm';
                html += '</button>';
                html += '</div>';
                html += '</div>';
            }
        } else {
            // Không có tin tức
            html += '<div class="alert alert-warning text-center">';
            html += '<h4><i class="fas fa-folder-open mr-2"></i>📭 Danh mục trống</h4>';
            html += '<p>Danh mục "<strong>' + escapeHtml(categoryName) + '</strong>" và các danh mục con chưa có tin tức nào.</p>';
            html += '<div class="mt-3">';
            html += '<a href="/News/Create?categoryId=' + currentCategoryId + '" class="btn btn-primary mr-2">';
            html += '<i class="fas fa-plus mr-1"></i>Thêm tin tức mới';
            html += '</a>';
            html += '<button class="btn btn-outline-info" onclick="showCategoryStatistics(' + currentCategoryId + ')">';
            html += '<i class="fas fa-chart-bar mr-1"></i>Xem thống kê';
            html += '</button>';
            html += '</div>';
            html += '</div>';
        }

        // Navigation buttons với thông tin tổng quan
        html += '<div class="card mt-4">';
        html += '<div class="card-body">';
        html += '<div class="row">';

        // Previous category
        html += '<div class="col-md-4">';
        if (currentCategoryId > 1) {
            html += '<button onclick="loadCategoryNews(' + (currentCategoryId - 1) + ')" class="btn btn-outline-secondary btn-block">';
            html += '<i class="fas fa-chevron-left mr-1"></i>Danh mục ' + (currentCategoryId - 1);
            html += '</button>';
        }
        html += '</div>';

        // Reload current
        html += '<div class="col-md-4">';
        html += '<button onclick="loadCategoryNews(' + currentCategoryId + ')" class="btn btn-primary btn-block">';
        html += '<i class="fas fa-redo mr-1"></i>Tải lại danh mục';
        html += '</button>';
        html += '</div>';

        // Next category
        html += '<div class="col-md-4">';
        html += '<button onclick="loadCategoryNews(' + (currentCategoryId + 1) + ')" class="btn btn-outline-secondary btn-block">';
        html += 'Danh mục ' + (currentCategoryId + 1) + '<i class="fas fa-chevron-right ml-1"></i>';
        html += '</button>';
        html += '</div>';

        html += '</div>';
        html += '</div>';
        html += '</div>';

        var contentContainer = $('#content-container');
        if (contentContainer.length > 0) {
            contentContainer.html(html);
            console.log('✅ Content updated successfully');

            // Enable tooltips
            $('[title]').tooltip();

            // Scroll to content
            $('html, body').animate({
                scrollTop: contentContainer.offset().top - 100
            }, 500);
        } else {
            console.error('❌ #content-container not found');
        }
    } catch (error) {
        console.error('Error in displayCategoryNews:', error);
    }
}
function showCategoryStatistics(categoryId) {
    // TODO: Implement category statistics
    alert('Tính năng thống kê danh mục sẽ được phát triển sau!\n\nSẽ hiển thị:\n- Số tin theo từng danh mục con\n- Biểu đồ phân bố\n- Xu hướng theo thời gian');
}

function toggleNewsMode(categoryId, includeSubcategories) {
    console.log('🔄 Toggle news mode:', categoryId, includeSubcategories);

    updateHeader('Đang chuyển đổi chế độ xem...');
    showLoadingInContent();

    $.ajax({
        url: '/Home/GetNewsByCategory',
        type: 'GET',
        data: {
            categoryId: categoryId,
            page: 1,
            pageSize: 20,
            includeSubcategories: includeSubcategories
        },
        dataType: 'json',
        success: function (data) {
            console.log('✅ News mode toggled:', data);

            if (data.success) {
                var modeText = includeSubcategories ? 'bao gồm danh mục con' : 'chỉ danh mục hiện tại';
                updateHeader('📁 ' + data.categoryName + ' (' + modeText + ')');

                if (data.data && data.data.length > 0) {
                    displayCategoryNews(data.data, data.categoryName, data.totalCount,
                        data.includeSubcategories, data.subcategoryCount);
                } else {
                    showNoCategoryNews(data.categoryName);
                }
            } else {
                showCategoryError(data.categoryName || 'Unknown');
            }
        },
        error: function (xhr, status, error) {
            console.error('❌ Error toggling news mode:', error);
            showCategoryError('Lỗi chuyển đổi chế độ xem');
        }
    });
}


function generateNewsCard(news, showCategories) {
    if (typeof showCategories === 'undefined') {
        showCategories = false;
    }

    var html = '<div class="col-lg-4 col-md-6 mb-4">';
    html += '<div class="card news-card h-100 shadow-sm">';
    html += '<div class="card-body d-flex flex-column">';

    // Title
    html += '<h5 class="news-title mb-3">';
    html += '<a href="/News/Details/' + news.Id + '" class="text-decoration-none">';
    html += escapeHtml(news.Title);
    html += '</a>';
    html += '</h5>';

    // Categories (if enabled)
    if (showCategories && news.Categories && news.Categories.length > 0) {
        html += '<div class="mb-2">';
        for (var j = 0; j < news.Categories.length; j++) {
            html += '<span class="badge badge-secondary mr-1 mb-1">';
            html += escapeHtml(news.Categories[j]);
            html += '</span>';
        }
        html += '</div>';
    }

    // Summary
    if (news.Summary) {
        html += '<p class="news-summary text-muted flex-grow-1">';
        html += escapeHtml(news.Summary);
        html += '</p>';
    }

    // Meta info
    html += '<div class="news-meta mt-auto pt-3 border-top">';
    html += '<div class="d-flex justify-content-between align-items-center">';
    html += '<small class="text-muted">';
    html += '<i class="far fa-calendar-alt mr-1"></i>';
    html += escapeHtml(news.CreatedDate);
    html += '</small>';
    html += '<a href="/News/Details/' + news.Id + '" class="btn btn-sm btn-outline-primary">';
    html += '<i class="fas fa-eye mr-1"></i>Xem';
    html += '</a>';
    html += '</div>';
    html += '</div>';

    html += '</div></div></div>';

    return html;
}

// ===== CATEGORY MANAGEMENT =====
function displayCategories(categories) {
    console.log('🔍 displayCategories called with:', categories);

    if (!categories || !Array.isArray(categories)) {
        console.error('❌ Invalid categories data:', categories);
        showErrorCategories();
        return;
    }

    try {
        var html = '<div class="categories-menu">';
        var processedIds = new Set();

        for (var i = 0; i < categories.length; i++) {
            var category = categories[i];

            if (!category.Id || !category.Name || processedIds.has(category.Id)) {
                console.warn('⚠️ Invalid or duplicate category:', category);
                continue;
            }

            processedIds.add(category.Id);

            html += '<div class="category-item" data-category-level="0">';

            // Root category link với thông tin chi tiết hơn
            html += '<div class="category-link" ';
            html += 'data-category-id="' + category.Id + '" ';
            html += 'data-category-name="' + escapeHtml(category.Name) + '" ';
            html += 'data-level="0">';
            html += '<div class="category-content">';
            html += '<div class="d-flex align-items-center">';
            html += '<div class="category-icon text-primary"><i class="fas fa-folder"></i></div>';
            html += '<div class="category-name font-weight-bold">' + escapeHtml(category.Name) + '</div>';
            html += '</div>';
            html += '<div class="category-meta">';

            // ✅ Hiển thị tổng số tin với tooltip
            var totalNews = category.NewsCount || 0;
            var tooltipText = 'Tổng ' + totalNews + ' tin tức (bao gồm cả danh mục con)';

            html += '<span class="news-count badge badge-primary" title="' + tooltipText + '">';
            html += '<i class="fas fa-newspaper mr-1"></i>' + totalNews;
            html += '</span>';

            if (category.HasChildren) {
                html += '<button class="category-toggle-btn toggle-btn btn btn-sm btn-outline-secondary ml-2" ';
                html += 'data-parent-id="' + category.Id + '" data-level="0" ';
                html += 'title="Mở rộng để xem danh mục con">';
                html += '<i class="fas fa-chevron-right"></i>';
                html += '</button>';
            }

            html += '</div>';
            html += '</div></div>';

            if (category.HasChildren) {
                html += '<div id="subcategories-' + category.Id + '" class="subcategories-container" style="display: none;"></div>';
            }

            html += '</div>';
        }

        html += '</div>';

        console.log('✅ Root categories HTML generated');

        var categoriesMenu = $('#categories-menu');
        if (categoriesMenu.length > 0) {
            categoriesMenu.html(html);
            attachCategoryEventListeners();

            // Enable tooltips
            $('[title]').tooltip();

            setTimeout(function () {
                if (processedIds.has(1)) {
                    setActiveCategory(1);
                    console.log('✅ Set category 1 as active');
                }
            }, 500);
        } else {
            console.error('❌ #categories-menu not found');
        }
    } catch (error) {
        console.error('Error in displayCategories:', error);
        showErrorCategories();
    }
}
function displaySubcategories(parentId, subcategories, level) {
    var container = $('#subcategories-' + parentId);
    var html = '';

    for (var i = 0; i < subcategories.length; i++) {
        var sub = subcategories[i];
        if (!sub.Id || !sub.Name) continue;

        var linkClass = getCategoryLinkClass(level);
        var iconClass = getCategoryIconClass(level);
        var indentClass = 'category-level-' + level;

        html += '<div class="category-item ' + indentClass + '" data-category-level="' + level + '">';

        html += '<div class="' + linkClass + '" ';
        html += 'data-category-id="' + sub.Id + '" ';
        html += 'data-category-name="' + escapeHtml(sub.Name) + '" ';
        html += 'data-level="' + level + '">';

        html += '<div class="category-content">';
        html += '<div class="d-flex align-items-center flex-grow-1">';
        html += '<div class="category-icon"><i class="' + iconClass + '"></i></div>';
        html += '<div class="category-name">' + escapeHtml(sub.Name) + '</div>';
        html += '</div>';

        html += '<div class="category-meta">';

        // ✅ Hiển thị tổng số tin với tooltip cho danh mục con
        var totalNews = sub.NewsCount || 0;
        var tooltipText = 'Tổng ' + totalNews + ' tin tức (bao gồm cả danh mục con)';

        html += '<span class="news-count badge badge-secondary" title="' + tooltipText + '">';
        html += '<i class="fas fa-newspaper mr-1"></i>' + totalNews;
        html += '</span>';

        if (sub.HasChildren && level < 7) {
            html += '<button class="category-toggle-btn toggle-btn ml-2" ';
            html += 'data-parent-id="' + sub.Id + '" ';
            html += 'data-level="' + level + '" ';
            html += 'title="Mở rộng để xem danh mục con" ';
            html += 'type="button">';
            html += '<i class="fas fa-chevron-right"></i>';
            html += '</button>';
        }

        html += '</div>';
        html += '</div>';
        html += '</div>';

        if (sub.HasChildren && level < 7) {
            html += '<div id="subcategories-' + sub.Id + '" class="subcategories-container" style="display: none;"></div>';
        }

        html += '</div>';
    }

    container.html(html);

    // Enable tooltips cho các element mới
    container.find('[title]').tooltip();

    attachSubcategoryEventListeners(container);
}

function attachCategoryEventListeners() {
    console.log('📎 Attaching category event listeners...');

    try {
        // Updated event listener to use loadCategoryNews
        $('.category-link, .subcategory-link, .sub-subcategory-link, .level-4-link, .level-5-link, .level-6-link, .level-7-link, .level-8-link').off('click').on('click', function (e) {
            if ($(e.target).closest('.toggle-btn').length) {
                return;
            }

            var categoryId = $(this).attr('data-category-id');
            var categoryName = $(this).attr('data-category-name');

            console.log('🖱️ Category clicked:', {
                element: this,
                categoryId: categoryId,
                categoryName: categoryName
            });

            if (categoryId && categoryName) {
                // Use the new function
                loadCategoryNews(parseInt(categoryId));
            } else {
                console.error('❌ Missing category data on click');
            }
        });

        $('.toggle-btn').off('click').on('click', function (e) {
            e.stopPropagation();
            var parentId = $(this).attr('data-parent-id');
            var level = $(this).attr('data-level') || 0;
            if (parentId) {
                toggleCategory(parseInt(parentId), parseInt(level));
            }
        });

        console.log('✅ Event listeners attached to ' + $('.category-link').length + ' category links');
    } catch (error) {
        console.error('Error in attachCategoryEventListeners:', error);
    }
}

// ===== SEARCH FUNCTIONS =====
function performSearch() {
    var query = $('#search-input').val().trim();
    if (query.length < 3) { // Cập nhật thông báo
        alert('Vui lòng nhập ít nhất 3 ký tự để tìm kiếm');
        return;
    }

    // Hiển thị trạng thái đang tìm
    showSearchIndicator(true);

    updateHeader('Tìm kiếm: "' + query + '"');
    showLoadingInContent();

    $.ajax({
        url: '/Home/QuickSearch',
        type: 'GET',
        data: { term: query, maxResults: 20 },
        dataType: 'json',
        success: function (data) {
            showSearchIndicator(false);
            if (data.success && data.results && data.results.length > 0) {
                displaySearchResults(data.results, query);
            } else {
                showNoSearchResults(query);
            }
        },
        error: function (xhr, status, error) {
            showSearchIndicator(false);
            console.error('Search error:', error);
            showSearchError();
        }
    });
}
function showSearchIndicator(isSearching) {
    var $searchBtn = $('button[onclick="performSearch()"]');
    var $searchInput = $('#search-input');

    if (isSearching) {
        $searchBtn.html('<i class="fa fa-spinner fa-spin mr-1"></i>Đang tìm...');
        $searchBtn.prop('disabled', true);
        $searchInput.addClass('searching');
    } else {
        $searchBtn.html('<i class="fa fa-search mr-1"></i>Tìm kiếm');
        $searchBtn.prop('disabled', false);
        $searchInput.removeClass('searching');
    }
}

function clearSearch() {
    try {
        $('#search-input').val('');
        currentMode = 'category';

        // Return to current category or default to category 1
        var categoryToLoad = currentCategoryId || 1;
        loadCategoryNews(categoryToLoad);
    } catch (error) {
        console.error('Error in clearSearch:', error);
    }
}

// ===== SUPPORT FUNCTIONS =====
function setActiveCategory(categoryId) {
    try {
        $('.category-link, .subcategory-link, .sub-subcategory-link, .level-4-link, .level-5-link, .level-6-link, .level-7-link, .level-8-link')
            .removeClass('active category-loading-state category-active-pulse');

        var $activeCategory = $('[data-category-id="' + categoryId + '"]');
        $activeCategory.addClass('active category-active-pulse');

        var $categoryMenu = $('#categories-menu');
        if ($activeCategory.length && $categoryMenu.length) {
            var categoryTop = $activeCategory.position().top;
            var menuHeight = $categoryMenu.height();
            var menuScrollTop = $categoryMenu.scrollTop();

            if (categoryTop < 0 || categoryTop > menuHeight) {
                $categoryMenu.animate({
                    scrollTop: menuScrollTop + categoryTop - menuHeight / 2
                }, 300);
            }
        }
    } catch (error) {
        console.error('Error in setActiveCategory:', error);
    }
}

function updateHeader(title) {
    try {
        var headerElement = $('#content-title');
        if (headerElement.length > 0) {
            headerElement.html('<i class="fa fa-newspaper mr-2"></i>' + title);
        }
    } catch (error) {
        console.error('Error in updateHeader:', error);
    }
}

function updateSubtitle(subtitle) {
    try {
        var subtitleElement = $('#content-subtitle');
        if (subtitleElement.length > 0) {
            subtitleElement.text(subtitle);
        }
    } catch (error) {
        console.error('Error in updateSubtitle:', error);
    }
}

function showCategoryLoading(categoryName) {
    try {
        var html = '<div class="category-loading-container text-center">';
        html += '<div class="mb-4">';
        html += '<div class="spinner-border text-primary large-spinner" role="status">';
        html += '<span class="sr-only">Loading...</span>';
        html += '</div>';
        html += '</div>';
        html += '<h4 class="text-primary mb-3">';
        html += '<i class="fas fa-folder-open mr-2"></i>';
        html += 'Đang tải tin tức từ danh mục';
        html += '</h4>';
        html += '<p class="lead text-muted mb-4">';
        html += '"<strong>' + escapeHtml(categoryName) + '</strong>"';
        html += '</p>';
        html += '<div class="loading-dots mb-4">';
        html += '<span class="dot"></span>';
        html += '<span class="dot"></span>';
        html += '<span class="dot"></span>';
        html += '</div>';
        html += '</div>';

        var contentContainer = $('#content-container');
        if (contentContainer.length > 0) {
            contentContainer.html(html);
        }
    } catch (error) {
        console.error('Error in showCategoryLoading:', error);
    }
}

function addCategoryLoadingState(categoryId) {
    try {
        $('[data-category-id="' + categoryId + '"]').addClass('category-loading-state');
    } catch (error) {
        console.error('Error in addCategoryLoadingState:', error);
    }
}

function removeCategoryLoadingState(categoryId) {
    try {
        $('[data-category-id="' + categoryId + '"]').removeClass('category-loading-state');
    } catch (error) {
        console.error('Error in removeCategoryLoadingState:', error);
    }
}

function showSuccessMessage(message) {
    try {
        var html = '<div class="alert alert-success success-message">';
        html += '<div class="d-flex align-items-center">';
        html += '<i class="fas fa-check-circle mr-2" style="font-size: 1.2rem;"></i>';
        html += '<div>';
        html += '<strong>Thành công!</strong><br>';
        html += '<small>' + escapeHtml(message) + '</small>';
        html += '</div>';
        html += '</div>';
        html += '</div>';

        $('body').append(html);

        setTimeout(function () {
            $('.success-message').fadeOut(300, function () {
                $(this).remove();
            });
        }, 3000);
    } catch (error) {
        console.error('Error in showSuccessMessage:', error);
    }
}

function showErrorMessage(message) {
    try {
        var html = '<div class="alert alert-danger error-message">';
        html += '<div class="d-flex align-items-center">';
        html += '<i class="fas fa-exclamation-triangle mr-2" style="font-size: 1.2rem;"></i>';
        html += '<div>';
        html += '<strong>Lỗi!</strong><br>';
        html += '<small>' + escapeHtml(message) + '</small>';
        html += '</div>';
        html += '</div>';
        html += '</div>';

        $('body').append(html);

        setTimeout(function () {
            $('.error-message').fadeOut(300, function () {
                $(this).remove();
            });
        }, 5000);
    } catch (error) {
        console.error('Error in showErrorMessage:', error);
    }
}

// ===== ADDITIONAL FUNCTIONS =====
function loadMoreNews() {
    // TODO: Implement pagination for loading more news
    alert('Tính năng tải thêm tin tức sẽ được phát triển sau');
}

function loadCategories() {
    console.log('Loading categories...');
    try {
        $.ajax({
            url: '/Home/GetCategoriesTree',
            type: 'GET',
            dataType: 'json',
            success: function (data) {
                if (data.success && data.categories) {
                    displayCategories(data.categories);
                } else {
                    showEmptyCategories();
                }
            },
            error: function (xhr, status, error) {
                console.error('Error loading categories:', error);
                showErrorCategories();
            }
        });
    } catch (error) {
        console.error('Error in loadCategories:', error);
        showErrorCategories();
    }
}

// ===== PLACEHOLDER FUNCTIONS =====
function showLoading() {
    try {
        var contentContainer = $('#content-container');
        if (contentContainer.length > 0) {
            contentContainer.html(
                '<div class="loading-spinner">' +
                '<div class="spinner-border text-primary"></div>' +
                '<p>Đang tải...</p>' +
                '</div>'
            );
        } else {
            console.error('❌ #content-container not found in showLoading');
        }
    } catch (error) {
        console.error('Error in showLoading:', error);
    }
}

function displaySearchResults(results, query) {
    try {
        var html = '<div class="alert alert-info">';
        html += '<strong><i class="fas fa-search mr-2"></i>Tìm thấy ' + results.length + ' kết quả cho từ khóa: "' + escapeHtml(query) + '"</strong>';
        html += '</div><div class="row">';

        for (var i = 0; i < results.length; i++) {
            html += generateNewsCard(results[i]);
        }

        html += '</div>';
        html += '<div class="text-center mt-4">';
        html += '<button onclick="clearSearch()" class="btn btn-outline-secondary btn-lg">';
        html += '<i class="fa fa-arrow-left mr-1"></i> Quay lại danh mục hiện tại';
        html += '</button>';
        html += '</div>';

        var contentContainer = $('#content-container');
        if (contentContainer.length > 0) {
            contentContainer.html(html);
        }
    } catch (error) {
        console.error('Error in displaySearchResults:', error);
    }
}

function showNoSearchResults(query) {
    try {
        var contentContainer = $('#content-container');
        if (contentContainer.length > 0) {
            contentContainer.html(
                '<div class="alert alert-warning text-center">' +
                '<h4><i class="fa fa-search"></i> Không tìm thấy kết quả</h4>' +
                '<p>Không có tin tức nào chứa từ khóa: "<strong>' + escapeHtml(query) + '</strong>"</p>' +
                '<button onclick="clearSearch()" class="btn btn-primary">Quay lại danh mục hiện tại</button>' +
                '</div>'
            );
        }
    } catch (error) {
        console.error('Error in showNoSearchResults:', error);
    }
}

function showSearchError(message) {
    try {
        var contentContainer = $('#content-container');
        if (contentContainer.length > 0) {
            contentContainer.html(
                '<div class="alert alert-danger text-center">' +
                '<h4><i class="fa fa-exclamation-triangle"></i> Lỗi tìm kiếm</h4>' +
                '<p>' + escapeHtml(message) + '</p>' +
                '<button onclick="clearSearch()" class="btn btn-primary">Quay lại danh mục hiện tại</button>' +
                '</div>'
            );
        }
    } catch (error) {
        console.error('Error in showSearchError:', error);
    }
}

function showNoCategoryNews(categoryName) {
    try {
        var contentContainer = $('#content-container');
        if (contentContainer.length > 0) {
            contentContainer.html(
                '<div class="alert alert-info text-center">' +
                '<h4><i class="fa fa-folder-open"></i> Danh mục trống</h4>' +
                '<p>Danh mục "<strong>' + escapeHtml(categoryName) + '</strong>" chưa có tin tức nào.</p>' +
                '<div class="mt-3">' +
                '<a href="/News/Create" class="btn btn-primary mr-2">' +
                '<i class="fa fa-plus"></i> Thêm tin tức mới</a>' +
                '</div>' +
                '</div>'
            );
        }
    } catch (error) {
        console.error('Error in showNoCategoryNews:', error);
    }
}

function showCategoryError(categoryName) {
    try {
        var contentContainer = $('#content-container');
        if (contentContainer.length > 0) {
            contentContainer.html(
                '<div class="alert alert-danger text-center">' +
                '<h4><i class="fa fa-exclamation-triangle"></i> Lỗi tải tin tức</h4>' +
                '<p>Không thể tải tin tức từ danh mục: "<strong>' + escapeHtml(categoryName) + '</strong>"</p>' +
                '<div class="mt-3">' +
                '<button onclick="loadCategories()" class="btn btn-warning mr-2">' +
                '<i class="fa fa-refresh"></i> Thử lại</button>' +
                '<button onclick="loadCategoryNews(1)" class="btn btn-secondary">' +
                '<i class="fa fa-arrow-left"></i> Về danh mục 1</button>' +
                '</div>' +
                '</div>'
            );
        }
    } catch (error) {
        console.error('Error in showCategoryError:', error);
    }
}

function showEmptyCategories() {
    try {
        var categoriesMenu = $('#categories-menu');
        if (categoriesMenu.length > 0) {
            categoriesMenu.html(
                '<div class="text-center p-3">' +
                '<p class="text-muted mb-2">Chưa có danh mục nào</p>' +
                '<a href="/Category/Create" class="btn btn-sm btn-success">' +
                '<i class="fa fa-plus"></i> Thêm danh mục</a>' +
                '</div>'
            );
        }
    } catch (error) {
        console.error('Error in showEmptyCategories:', error);
    }
}

function showErrorCategories() {
    try {
        var categoriesMenu = $('#categories-menu');
        if (categoriesMenu.length > 0) {
            categoriesMenu.html(
                '<div class="text-center p-3">' +
                '<p class="text-muted mb-2">Không thể tải danh mục</p>' +
                '<button onclick="loadCategories()" class="btn btn-sm btn-secondary">' +
                '<i class="fa fa-refresh"></i> Thử lại</button>' +
                '</div>'
            );
        }
    } catch (error) {
        console.error('Error in showErrorCategories:', error);
    }
}

function escapeHtml(text) {
    if (!text) return '';
    try {
        var div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    } catch (error) {
        console.error('Error in escapeHtml:', error);
        return text.toString();
    }
}

// ===== ADDITIONAL HELPER FUNCTIONS =====
function loadSubcategories(parentId, level) {
    if (typeof level === 'undefined') {
        level = 1;
    }

    console.log('🔄 loadSubcategories:', { parentId, level });

    try {
        var container = $('#subcategories-' + parentId);
        if (container.length === 0) {
            console.error('❌ Container not found for parentId:', parentId);
            return;
        }

        $.ajax({
            url: '/Home/GetSubcategories',
            type: 'GET',
            data: { parentId: parentId },
            dataType: 'json',
            timeout: 10000,
            success: function (data) {
                console.log('✅ Subcategories loaded:', data);

                if (data.success && data.subcategories && data.subcategories.length > 0) {
                    var html = '';

                    for (var i = 0; i < data.subcategories.length; i++) {
                        var sub = data.subcategories[i];

                        if (!sub.Id || !sub.Name) {
                            console.warn('⚠️ Invalid subcategory:', sub);
                            continue;
                        }

                        var linkClass = getCategoryLinkClass(level);
                        var iconClass = getCategoryIconClass(level);

                        // ===== CRITICAL: Each subcategory must be a complete block =====
                        html += '<div class="category-item" data-category-level="' + level + '">';

                        // Subcategory link
                        html += '<div class="' + linkClass + '" ';
                        html += 'data-category-id="' + sub.Id + '" ';
                        html += 'data-category-name="' + escapeHtml(sub.Name) + '" ';
                        html += 'data-level="' + level + '">';

                        html += '<div class="category-content">';
                        html += '<div class="d-flex align-items-center flex-grow-1">';
                        html += '<div class="category-icon"><i class="' + iconClass + '"></i></div>';
                        html += '<div class="category-name">' + escapeHtml(sub.Name) + '</div>';
                        html += '</div>';

                        html += '<div class="category-meta">';
                        html += '<span class="news-count">' + (sub.NewsCount || 0) + '</span>';

                        if (sub.HasChildren && level < 5) {
                            html += '<button class="category-toggle-btn toggle-btn ml-2" ';
                            html += 'data-parent-id="' + sub.Id + '" ';
                            html += 'data-level="' + level + '" ';
                            html += 'type="button">';
                            html += '<i class="fas fa-chevron-right"></i>';
                            html += '</button>';
                        }

                        html += '</div>'; // End category-meta
                        html += '</div>'; // End category-content
                        html += '</div>'; // End category link

                        // ===== CRITICAL: Subcategory container INSIDE the same category-item =====
                        if (sub.HasChildren && level < 5) {
                            html += '<div id="subcategories-' + sub.Id + '" class="subcategories-container" style="display: none;"></div>';
                        }

                        html += '</div>'; // End category-item - VERY IMPORTANT!
                    }

                    // Update container content
                    container.html(html);

                    // Attach event listeners to new elements
                    attachSubcategoryEventListeners(container, level);

                    console.log('✅ Subcategories rendered for level', level, '- Total items:', data.subcategories.length);
                } else {
                    container.html('<div class="text-center p-2"><small class="text-muted">Không có danh mục con</small></div>');
                }
            },
            error: function (xhr, status, error) {
                console.error('❌ Error loading subcategories:', error);

                var errorHtml = '<div class="text-center p-2">' +
                    '<small class="text-danger">' +
                    '<i class="fas fa-exclamation-triangle mr-1"></i>' +
                    'Lỗi tải danh mục con' +
                    '</small>' +
                    '<br>' +
                    '<button onclick="loadSubcategories(' + parentId + ', ' + level + ')" ' +
                    'class="btn btn-sm btn-outline-danger mt-1">' +
                    '<i class="fas fa-redo mr-1"></i>Thử lại' +
                    '</button>' +
                    '</div>';
                container.html(errorHtml);
            }
        });
    } catch (error) {
        console.error('Error in loadSubcategories:', error);
    }
}

function attachSubcategoryEventListeners(container, level) {
    try {
        container.find('.subcategory-link, .sub-subcategory-link, .level-4-link, .level-5-link, .level-6-link, .level-7-link, .level-8-link').off('click').on('click', function (e) {
            if ($(e.target).closest('.toggle-btn').length) {
                return;
            }

            var categoryId = $(this).attr('data-category-id');
            var categoryName = $(this).attr('data-category-name');

            if (categoryId && categoryName) {
                loadCategoryNews(parseInt(categoryId));
            }
        });

        container.find('.toggle-btn').off('click').on('click', function (e) {
            e.stopPropagation();
            var parentId = $(this).attr('data-parent-id');
            var nextLevel = $(this).attr('data-level') || level + 1;
            if (parentId) {
                toggleCategory(parseInt(parentId), parseInt(nextLevel));
            }
        });
    } catch (error) {
        console.error('Error in attachSubcategoryEventListeners:', error);
    }
}

function getCategoryLinkClass(level) {
    switch (level) {
        case 1: return 'subcategory-link';
        case 2: return 'sub-subcategory-link';
        case 3: return 'level-4-link';
        case 4: return 'level-5-link';
        case 5: return 'level-6-link';
        case 6: return 'level-7-link';
        case 7: return 'level-8-link';
        default: return 'level-8-link';
    }
}

function getCategoryIconClass(level) {
    switch (level) {
        case 1: return 'fas fa-folder-open';
        case 2: return 'fas fa-file-alt';
        case 3: return 'fas fa-file';
        case 4: return 'fas fa-circle';
        default: return 'fas fa-dot-circle';
    }
}

function toggleCategory(categoryId, level) {
    if (typeof level === 'undefined') {
        level = 0;
    }

    try {
        var container = $('#subcategories-' + categoryId);
        var toggleBtn = $('[data-parent-id="' + categoryId + '"]');

        if (container.hasClass('expanded')) {
            container.removeClass('expanded').hide();
            toggleBtn.removeClass('expanded').find('i').removeClass('fa-chevron-down').addClass('fa-chevron-right');
        } else {
            container.html('<div class="text-center p-3"><small class="text-muted"><i class="fa fa-spinner fa-spin"></i> Đang tải...</small></div>');
            container.addClass('expanded').show();
            toggleBtn.addClass('expanded').find('i').removeClass('fa-chevron-right').addClass('fa-chevron-down');
            loadSubcategories(categoryId, level + 1);
        }
    } catch (error) {
        console.error('Error in toggleCategory:', error);
    }
}

// ===== MAIN INITIALIZATION =====
console.log('📦 Fixed JavaScript module loaded successfully');
console.log('🔧 Key functions defined:', typeof displayCategoryNews, typeof loadCategoryNews);