// ===== UPDATED HOME PAGE JAVASCRIPT - Auto load category 1 on start =====

// Global variables
var currentMode = 'category';
var currentCategoryId = 1; // Default to category 1
var currentCategoryName = '';
var searchTimeout;

// Initialize page
$(document).ready(function () {
    console.log('Page loaded, initializing...');
    loadCategories(); // Load categories first

    // Wait a bit then load category 1 news
    setTimeout(function () {
        loadCategoryNews(1); // Auto load category 1 news
    }, 1000);

    // Search input events
    $('#search-input').on('keyup', function (e) {
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
    });
});

// ===== MAIN FUNCTION: Load news by category ID =====
function loadCategoryNews(categoryId) {
    console.log('🔄 loadCategoryNews called with categoryId:', categoryId);

    if (!categoryId) {
        console.error('❌ Missing categoryId');
        return;
    }

    // Find category name from loaded categories
    var categoryName = findCategoryNameById(categoryId);
    if (!categoryName) {
        categoryName = 'Danh mục ' + categoryId;
    }

    console.log('📁 Loading news for category:', categoryId, '-', categoryName);

    // Update global state
    currentCategoryId = categoryId;
    currentCategoryName = categoryName;
    currentMode = 'category';

    // Set active state
    setActiveCategory(categoryId);

    // Update UI
    updateHeader('Tin tức ở danh mục: ' + categoryName);
    updateSubtitle('Đang tải tin tức...');
    showCategoryLoading(categoryName);

    // Add loading state
    addCategoryLoadingState(categoryId);

    // Make AJAX request
    $.ajax({
        url: '/Home/GetNewsByCategory',
        type: 'GET',
        data: {
            categoryId: categoryId,
            page: 1,
            pageSize: 20
        },
        dataType: 'json',
        timeout: 15000,
        success: function (data) {
            console.log('✅ Category news loaded:', data);

            // Remove loading state
            removeCategoryLoadingState(categoryId);

            if (data.success) {
                if (data.data && data.data.length > 0) {
                    console.log('📊 Successfully loaded ' + data.data.length + ' news items');

                    // Update header
                    updateHeader('Tin tức ở danh mục: ' + categoryName);
                    updateSubtitle('Tìm thấy ' + data.totalCount + ' tin tức');

                    // Display news
                    displayCategoryNews(data.data, categoryName, data.totalCount);

                    // Success notification
                    showSuccessMessage('Đã tải ' + data.data.length + ' tin tức từ "' + categoryName + '"');
                } else {
                    console.log('ℹ️ No news found for this category');
                    updateHeader('Tin tức ở danh mục: ' + categoryName);
                    updateSubtitle('Danh mục này chưa có tin tức');
                    showNoCategoryNews(categoryName);
                }
            } else {
                console.error('❌ API returned error:', data.message);
                updateHeader('Lỗi tải danh mục: ' + categoryName);
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

            // Remove loading state
            removeCategoryLoadingState(categoryId);

            var errorMsg = 'Lỗi kết nối';
            if (xhr.status === 404) {
                errorMsg = 'API không tìm thấy';
            } else if (xhr.status === 500) {
                errorMsg = 'Lỗi server';
            } else if (status === 'timeout') {
                errorMsg = 'Hết thời gian chờ';
            }

            updateHeader('Lỗi tải danh mục: ' + categoryName);
            updateSubtitle('Không thể kết nối đến server');
            showCategoryError(categoryName + ' (' + errorMsg + ')');

            // Error notification
            showErrorMessage('Không thể tải tin tức từ "' + categoryName + '"');
        }
    });
}

// ===== HELPER: Find category name by ID =====
function findCategoryNameById(categoryId) {
    var categoryName = '';

    // Search in loaded category links
    $('[data-category-id="' + categoryId + '"]').each(function () {
        var name = $(this).attr('data-category-name');
        if (name) {
            categoryName = name;
            return false; // Break loop
        }
    });

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
function displayCategoryNews(newsArray, categoryName, totalCount) {
    console.log('✅ displayCategoryNews called with:', {
        newsCount: newsArray ? newsArray.length : 0,
        categoryName: categoryName,
        totalCount: totalCount
    });

    var html = '';

    // Header thông tin danh mục với style mới
    html += '<div class="alert alert-primary border-left-primary mb-4">';
    html += '<div class="d-flex align-items-center">';
    html += '<i class="fas fa-folder-open mr-3" style="font-size: 1.5rem;"></i>';
    html += '<div>';
    html += '<h5 class="mb-1"><strong>Tin tức ở danh mục: ' + escapeHtml(categoryName) + '</strong></h5>';
    html += '<small class="text-muted">Tìm thấy ' + totalCount + ' tin tức trong danh mục này</small>';
    html += '</div>';
    html += '</div>';
    html += '</div>';

    // Hiển thị tin tức
    if (newsArray && newsArray.length > 0) {
        html += '<div class="row">';

        for (var i = 0; i < newsArray.length; i++) {
            var news = newsArray[i];
            html += generateNewsCard(news, true);
        }

        html += '</div>';

        // Thông tin tổng kết
        if (totalCount > newsArray.length) {
            html += '<div class="alert alert-info mt-3">';
            html += '<i class="fas fa-info-circle mr-2"></i>';
            html += 'Hiển thị ' + newsArray.length + ' trong tổng số ' + totalCount + ' tin tức. ';
            html += '<button class="btn btn-sm btn-outline-primary ml-2" onclick="loadMoreNews()">Tải thêm</button>';
            html += '</div>';
        }
    } else {
        // Không có tin tức
        html += '<div class="alert alert-warning text-center">';
        html += '<h4><i class="fas fa-folder-open mr-2"></i>Danh mục trống</h4>';
        html += '<p>Danh mục "<strong>' + escapeHtml(categoryName) + '</strong>" chưa có tin tức nào.</p>';
        html += '<div class="mt-3">';
        html += '<a href="/News/Create" class="btn btn-primary mr-2">';
        html += '<i class="fas fa-plus mr-1"></i>Thêm tin tức mới';
        html += '</a>';
        html += '</div>';
        html += '</div>';
    }

    // Navigation buttons
    html += '<div class="text-center mt-4">';
    html += '<div class="btn-group" role="group">';

    // Previous category button
    if (currentCategoryId > 1) {
        html += '<button onclick="loadCategoryNews(' + (currentCategoryId - 1) + ')" class="btn btn-outline-secondary">';
        html += '<i class="fas fa-chevron-left mr-1"></i>Danh mục trước';
        html += '</button>';
    }

    // Reload current category
    html += '<button onclick="loadCategoryNews(' + currentCategoryId + ')" class="btn btn-outline-primary">';
    html += '<i class="fas fa-redo mr-1"></i>Tải lại';
    html += '</button>';

    // Next category button  
    html += '<button onclick="loadCategoryNews(' + (currentCategoryId + 1) + ')" class="btn btn-outline-secondary">';
    html += 'Danh mục tiếp<i class="fas fa-chevron-right ml-1"></i>';
    html += '</button>';

    html += '</div>';
    html += '</div>';

    // Update content
    $('#content-container').html(html);

    console.log('✅ Content updated successfully');

    // Scroll to content
    $('html, body').animate({
        scrollTop: $('#content-container').offset().top - 100
    }, 500);
}

function generateNewsCard(news, showCategories = false) {
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

    var html = '<div class="categories-menu">';
    var processedIds = new Set();

    for (var i = 0; i < categories.length; i++) {
        var category = categories[i];

        // Validate category data
        if (!category.Id || !category.Name || processedIds.has(category.Id)) {
            console.warn('⚠️ Invalid or duplicate category:', category);
            continue;
        }

        processedIds.add(category.Id);

        // ===== CRITICAL: Each root category is a complete block =====
        html += '<div class="category-item" data-category-level="0">';

        // Root category link
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
        html += '<span class="news-count badge badge-primary">' + (category.NewsCount || 0) + '</span>';

        if (category.HasChildren) {
            html += '<button class="category-toggle-btn toggle-btn btn btn-sm btn-outline-secondary ml-2" data-parent-id="' + category.Id + '" data-level="0">';
            html += '<i class="fas fa-chevron-right"></i>';
            html += '</button>';
        }

        html += '</div>';
        html += '</div></div>';

        // Container for subcategories - INSIDE the same category-item
        if (category.HasChildren) {
            html += '<div id="subcategories-' + category.Id + '" class="subcategories-container" style="display: none;"></div>';
        }

        html += '</div>'; // End category-item - VERY IMPORTANT!
    }

    html += '</div>';

    console.log('✅ Root categories HTML generated');
    $('#categories-menu').html(html);

    attachCategoryEventListeners();

    // Auto-set category 1 as active
    setTimeout(function () {
        if (processedIds.has(1)) {
            setActiveCategory(1);
            console.log('✅ Set category 1 as active');
        }
    }, 500);
}

function attachCategoryEventListeners() {
    console.log('📎 Attaching category event listeners...');

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
}

// ===== SEARCH FUNCTIONS =====
function performSearch() {
    var query = $('#search-input').val().trim();
    if (query.length < 2) {
        alert('Vui lòng nhập ít nhất 2 ký tự để tìm kiếm');
        return;
    }

    currentMode = 'search';
    updateHeader('Kết quả tìm kiếm: "' + query + '"');
    updateSubtitle('Tìm kiếm trong tiêu đề, trích ngắn và nội dung');
    showLoading();

    // Clear active category when searching
    $('.category-link, .subcategory-link, .sub-subcategory-link, .level-4-link, .level-5-link, .level-6-link, .level-7-link, .level-8-link').removeClass('active');

    $.ajax({
        url: '/Home/QuickSearch',
        type: 'GET',
        data: { term: query, maxResults: 20 },
        dataType: 'json',
        success: function (data) {
            if (data.success && data.results && data.results.length > 0) {
                displaySearchResults(data.results, query);
            } else {
                showNoSearchResults(query);
            }
        },
        error: function (xhr, status, error) {
            console.error('Search error:', error);
            showSearchError('Lỗi tìm kiếm: ' + error);
        }
    });
}

function clearSearch() {
    $('#search-input').val('');
    currentMode = 'category';

    // Return to current category or default to category 1
    var categoryToLoad = currentCategoryId || 1;
    loadCategoryNews(categoryToLoad);
}

// ===== SUPPORT FUNCTIONS =====
function setActiveCategory(categoryId) {
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
}

function updateHeader(title) {
    $('#content-title').html('<i class="fa fa-newspaper mr-2"></i>' + title);
}

function updateSubtitle(subtitle) {
    $('#content-subtitle').text(subtitle);
}

function showCategoryLoading(categoryName) {
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

    $('#content-container').html(html);
}

function addCategoryLoadingState(categoryId) {
    $('[data-category-id="' + categoryId + '"]').addClass('category-loading-state');
}

function removeCategoryLoadingState(categoryId) {
    $('[data-category-id="' + categoryId + '"]').removeClass('category-loading-state');
}

function showSuccessMessage(message) {
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
}

function showErrorMessage(message) {
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
}

// ===== ADDITIONAL FUNCTIONS =====
function loadMoreNews() {
    // TODO: Implement pagination for loading more news
    alert('Tính năng tải thêm tin tức sẽ được phát triển sau');
}

function loadCategories() {
    console.log('Loading categories...');
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
}

// ===== PLACEHOLDER FUNCTIONS (from original code) =====
function showLoading() {
    $('#content-container').html(
        '<div class="loading-spinner">' +
        '<div class="spinner-border text-primary"></div>' +
        '<p>Đang tải...</p>' +
        '</div>'
    );
}

function displaySearchResults(results, query) {
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

    $('#content-container').html(html);
}

function showNoSearchResults(query) {
    $('#content-container').html(
        '<div class="alert alert-warning text-center">' +
        '<h4><i class="fa fa-search"></i> Không tìm thấy kết quả</h4>' +
        '<p>Không có tin tức nào chứa từ khóa: "<strong>' + escapeHtml(query) + '</strong>"</p>' +
        '<button onclick="clearSearch()" class="btn btn-primary">Quay lại danh mục hiện tại</button>' +
        '</div>'
    );
}

function showSearchError(message) {
    $('#content-container').html(
        '<div class="alert alert-danger text-center">' +
        '<h4><i class="fa fa-exclamation-triangle"></i> Lỗi tìm kiếm</h4>' +
        '<p>' + escapeHtml(message) + '</p>' +
        '<button onclick="clearSearch()" class="btn btn-primary">Quay lại danh mục hiện tại</button>' +
        '</div>'
    );
}

function showNoCategoryNews(categoryName) {
    $('#content-container').html(
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

function showCategoryError(categoryName) {
    $('#content-container').html(
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

function showEmptyCategories() {
    $('#categories-menu').html(
        '<div class="text-center p-3">' +
        '<p class="text-muted mb-2">Chưa có danh mục nào</p>' +
        '<a href="/Category/Create" class="btn btn-sm btn-success">' +
        '<i class="fa fa-plus"></i> Thêm danh mục</a>' +
        '</div>'
    );
}

function showErrorCategories() {
    $('#categories-menu').html(
        '<div class="text-center p-3">' +
        '<p class="text-muted mb-2">Không thể tải danh mục</p>' +
        '<button onclick="loadCategories()" class="btn btn-sm btn-secondary">' +
        '<i class="fa fa-refresh"></i> Thử lại</button>' +
        '</div>'
    );
}

function escapeHtml(text) {
    if (!text) return '';
    var div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// ===== ADDITIONAL HELPER FUNCTIONS (from original code) =====
function loadSubcategories(parentId, level = 1) {
    console.log('🔄 loadSubcategories:', { parentId, level });

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
}

function attachSubcategoryEventListeners(container, level) {
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

function toggleCategory(categoryId, level = 0) {
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
}


// ===== MAIN INITIALIZATION =====
console.log('📦 Updated JavaScript module loaded successfully');
console.log('🔧 Key functions defined:', typeof displayCategoryNews, typeof loadCategoryNews);