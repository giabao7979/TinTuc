// ===================================
// COMPLETE HOME PAGE JAVASCRIPT
// ===================================

// ===== GLOBAL STATE & VARIABLES =====
let currentState = {
    categoryId: null,
    searchTerm: '',
    categorySearch: '',
    page: 1,
    sortBy: 'newest',
    expandedIds: []
};

let searchTimeouts = {};
let categoryCache = new Map(); // Cache for subcategories
let expandedState = new Set(); // Track expanded state
let isLoading = false;
var categorySearchTimeout;
var isSearchMode = true;

// ===== DOCUMENT READY =====
$(document).ready(function () {
    console.log('🚀 Initializing Advanced Home Page...');

    // Initialize state from server data
    initializeState();

    // Setup event handlers
    initializeEventHandlers();

    // Update URL to match current state
    updateUrl();

    // Debug info
    console.log('🎯 Initial state:', currentState);
});

// ===== INITIALIZATION =====
function initializeState() {
    // Get initial values from hidden form
    currentState.categoryId = $('#currentCategoryId').val() || null;
    currentState.searchTerm = $('#currentSearchTerm').val() || '';
    currentState.categorySearch = $('#currentCategorySearch').val() || '';
    currentState.page = parseInt($('#currentPage').val()) || 1;
    currentState.sortBy = $('#currentSortBy').val() || 'newest';

    // Get expanded IDs
    currentState.expandedIds = [];
    $('.expanded-category').each(function () {
        const id = parseInt($(this).val());
        if (id) {
            currentState.expandedIds.push(id);
            expandedState.add(id);
        }
    });

    // Set initial values in UI
    $('#newsSearchInput').val(currentState.searchTerm);
    $('#categorySearchInput').val(currentState.categorySearch);
    $('#sortBySelect').val(currentState.sortBy);

    console.log('✅ State initialized:', currentState);
}

function initializeEventHandlers() {
    console.log('🔗 Setting up event handlers...');

    // === CATEGORY TREE CONTROLS ===
    $('#expandAllBtn').click(expandAllCategories);
    $('#collapseAllBtn').click(collapseAllCategories);
    $('#refreshCategoriesBtn').click(refreshCategories);

    // === CATEGORY SEARCH ===
    $('#categorySearchInput').on('input', handleCategorySearch);
    $('#clearCategorySearch').click(clearCategorySearch);
    $('#searchCategoryBtn').click(performCategorySearch);

    // === NEWS SEARCH ===
    $('#newsSearchInput').on('input', handleNewsSearch);
    $('#clearNewsSearch').click(clearNewsSearch);
    $('#searchNewsBtn').click(performNewsSearch);

    // === SORT CHANGE ===
    $('#sortBySelect').change(function () {
        currentState.sortBy = $(this).val();
        currentState.page = 1;
        loadNews();
    });

    // === ENTER KEY HANDLERS ===
    $('#newsSearchInput').keypress(function (e) {
        if (e.which === 13) {
            e.preventDefault();
            performNewsSearch();
        }
    });

    $('#categorySearchInput').keypress(function (e) {
        if (e.which === 13) {
            e.preventDefault();
            performCategorySearch();
        }
    });

    // === DYNAMIC EVENT DELEGATION ===
    $(document).on('click', '.category-toggle-btn', handleCategoryToggle);
    $(document).on('click', '.category-selection', handleCategorySelection);
    $(document).on('click', '.pagination-link', handlePaginationClick);

    console.log('✅ Event handlers initialized');
}

// ===== CATEGORY TREE FUNCTIONS =====

function handleCategoryToggle(e) {
    e.preventDefault();
    e.stopPropagation();

    if (isLoading) {
        console.log('⏳ Already loading, skipping toggle');
        return;
    }

    const $btn = $(this);
    const categoryId = parseInt($btn.data('category-id'));
    const level = parseInt($btn.data('level')) || 0;

    console.log('🔄 Category toggle clicked:', categoryId, level);

    toggleSubcategories(categoryId, level + 1);
}

function toggleSubcategories(parentId, level) {
    console.log('🔄 Toggle subcategories:', parentId, level);

    var container = $('#subcategories-' + parentId);
    var toggleBtn = $(`[data-category-id="${parentId}"].category-toggle-btn`);
    var icon = toggleBtn.find('i');

    // Check if container exists
    if (container.length === 0) {
        console.error('❌ Container not found for parentId:', parentId);
        return;
    }

    if (expandedState.has(parentId)) {
        // COLLAPSE - chỉ ẩn, không xóa data
        console.log('🔽 Collapsing subcategories for:', parentId);

        expandedState.delete(parentId);
        currentState.expandedIds = currentState.expandedIds.filter(id => id !== parentId);

        container.removeClass('expanded').slideUp(250, function () {
            // Optional: clear content to save memory if too many items
            if (categoryCache.size > 20) {
                container.empty();
            }
        });

        icon.removeClass('fa-chevron-down').addClass('fa-chevron-right');
        toggleBtn.removeClass('expanded');

    } else {
        // EXPAND 
        console.log('🔼 Expanding subcategories for:', parentId);

        expandedState.add(parentId);
        if (!currentState.expandedIds.includes(parentId)) {
            currentState.expandedIds.push(parentId);
        }

        container.addClass('expanded');
        icon.removeClass('fa-chevron-right').addClass('fa-chevron-down');
        toggleBtn.addClass('expanded');

        // Kiểm tra cache trước
        if (categoryCache.has(parentId)) {
            // ✅ CÓ CACHE - Hiển thị ngay lập tức
            console.log('💾 Using cached data for:', parentId);
            const cachedHtml = categoryCache.get(parentId);
            container.html(cachedHtml).slideDown(250);

        } else {
            // 📡 CHƯA CÓ CACHE - Load với UX tối ưu
            console.log('📡 Loading new data for:', parentId);

            // Show elegant loading
            container.html(`
                <div class="loading-subcategories text-center py-3" style="background: rgba(0,123,255,0.03); border-radius: 6px; margin: 4px 0;">
                    <div class="d-flex align-items-center justify-content-center">
                        <div class="spinner-border spinner-border-sm text-primary mr-2" 
                             style="width: 1.2rem; height: 1.2rem; border-width: 2px;"></div>
                        <small class="text-muted">Đang tải danh mục con...</small>
                    </div>
                </div>
            `).slideDown(200);

            // Load subcategories
            loadSubcategoriesWithCache(parentId, level, container, toggleBtn);
        }
    }

    // Update URL to reflect expanded state
    updateUrl();
}

function loadSubcategoriesWithCache(parentId, level, container, toggleBtn) {
    isLoading = true;

    $.ajax({
        url: '/Home/GetSubcategories',
        type: 'GET',
        data: { parentId: parentId },
        dataType: 'json',
        timeout: 10000,
        cache: true,

        success: function (data) {
            console.log('✅ Subcategories API response for', parentId, ':', data);

            if (data.success && data.subcategories && data.subcategories.length > 0) {
                // Render subcategories
                var html = '';
                for (var i = 0; i < data.subcategories.length; i++) {
                    html += renderCategoryItem(data.subcategories[i], level);
                }

                // 💾 CACHE KẾT QUẢ
                categoryCache.set(parentId, html);

                // Update UI với animation smooth
                container.html(html);
                console.log('💾 Cached', data.subcategories.length, 'subcategories for parent:', parentId);

            } else if (data.success && (!data.subcategories || data.subcategories.length === 0)) {
                // Empty state
                var emptyHtml = `
                    <div class="empty-subcategories text-center py-3" style="background: #f8f9fa; border-radius: 6px; margin: 4px 0; border: 1px dashed #dee2e6;">
                        <small class="text-muted">
                            <i class="fas fa-info-circle mr-1 text-info"></i>
                            Danh mục này chưa có danh mục con
                        </small>
                    </div>
                `;

                // Cache empty state too
                categoryCache.set(parentId, emptyHtml);
                container.html(emptyHtml);

            } else {
                // API returned error
                console.error('❌ API Error:', data.message || 'Unknown error');
                showSubcategoryError(container, parentId, level, 'API Error: ' + (data.message || 'Unknown error'));
            }
        },

        error: function (xhr, status, error) {
            console.error('❌ AJAX Error loading subcategories:', {
                status: status,
                error: error,
                responseText: xhr.responseText,
                statusCode: xhr.status
            });

            // Don't cache errors - show retry option
            showSubcategoryError(container, parentId, level, 'Lỗi kết nối: ' + error);
        },

        complete: function () {
            isLoading = false;
        }
    });
}

function showSubcategoryError(container, parentId, level, errorMsg) {
    container.html(`
        <div class="error-subcategories text-center py-3" style="background: #f8d7da; border-radius: 6px; margin: 4px 0; border: 1px solid #f5c6cb;">
            <div class="mb-2">
                <small class="text-danger">
                    <i class="fas fa-exclamation-triangle mr-1"></i>
                    ${errorMsg}
                </small>
            </div>
            <button type="button" class="btn btn-sm btn-outline-danger"
                    onclick="retryLoadSubcategories(${parentId}, ${level})"
                    title="Thử tải lại">
                <i class="fas fa-redo mr-1"></i>Thử lại
            </button>
        </div>
    `);
}

function retryLoadSubcategories(parentId, level) {
    console.log('🔄 Retrying load for:', parentId);

    // Clear cache cho item này
    categoryCache.delete(parentId);

    var container = $('#subcategories-' + parentId);
    var toggleBtn = $(`[data-category-id="${parentId}"].category-toggle-btn`);

    // Show loading và retry
    container.html(`
        <div class="text-center py-2">
            <div class="spinner-border spinner-border-sm text-primary"></div>
            <small class="d-block mt-1 text-muted">Đang thử lại...</small>
        </div>
    `);

    loadSubcategoriesWithCache(parentId, level, container, toggleBtn);
}

function renderCategoryItem(category, level) {
    var linkClass = getCategoryLinkClass(level);
    var iconClass = getCategoryIconClass(level);

    console.log('🎨 Rendering category:', category.Name, 'Level:', level, 'HasChildren:', category.HasChildren);

    var html = `
        <div class="category-item" data-category-level="${level}" data-category-id="${category.Id}">
            <div class="${linkClass}"
                 data-category-id="${category.Id}"
                 data-category-name="${escapeHtml(category.Name)}"
                 data-level="${level}">
                <div class="category-content">
                    <div class="d-flex align-items-center flex-grow-1">
                        <div class="category-icon mr-2">
                            <i class="${iconClass}"></i>
                        </div>
                        <div class="category-name">
                            <a href="#" class="category-selection text-decoration-none"
                               data-category-id="${category.Id}" 
                               data-category-name="${escapeHtml(category.Name)}">
                                ${escapeHtml(category.Name)}
                            </a>
                        </div>
                    </div>
                    <div class="category-meta">
                        <span class="news-count">${category.NewsCount || 0}</span>
                        ${category.HasChildren ? `
                            <button type="button" class="category-toggle-btn ml-2 ${expandedState.has(category.Id) ? 'expanded' : ''}"
                                    data-category-id="${category.Id}"
                                    data-level="${level}"
                                    title="Mở rộng danh mục con">
                                <i class="fas fa-chevron-${expandedState.has(category.Id) ? 'down' : 'right'}"></i>
                            </button>
                        ` : ''}
                    </div>
                </div>
            </div>`;

    // Container cho subcategories
    if (category.HasChildren) {
        const isExpanded = expandedState.has(category.Id);
        const displayStyle = isExpanded ? 'block' : 'none';

        html += `<div id="subcategories-${category.Id}" 
                     class="subcategories-container ${isExpanded ? 'expanded' : ''}" 
                     style="display: ${displayStyle};">`;

        // Nếu đã expanded và có cache, render luôn
        if (isExpanded && categoryCache.has(category.Id)) {
            html += categoryCache.get(category.Id);
        }

        html += `</div>`;

        console.log('📁 Added subcategories container for:', category.Name, 'Expanded:', isExpanded);
    }

    html += `</div>`;
    return html;
}

function handleCategorySelection(e) {
    e.preventDefault();

    const categoryId = $(this).data('category-id');
    const categoryName = $(this).data('category-name');

    console.log('📂 Category selected:', categoryId, categoryName);

    // Update state
    currentState.categoryId = categoryId || null;
    currentState.searchTerm = '';
    currentState.page = 1;

    // Clear news search
    $('#newsSearchInput').val('');

    // Update content title
    updateContentTitle(categoryName || 'Tất cả tin tức');

    // Load news and update category tree
    loadNews();
    loadCategoryTree();
    updateUrl();
}

function expandAllCategories() {
    console.log('📂 Expanding all categories...');
    showLoading('#categoryTreeContainer');

    $.get('/Home/GetAllCategoryIds', function (data) {
        if (data.success && data.categoryIds) {
            console.log('📂 Got all category IDs:', data.categoryIds.length);

            // Add all to expanded state
            data.categoryIds.forEach(id => expandedState.add(id));
            currentState.expandedIds = data.categoryIds;

            loadCategoryTree();
        } else {
            console.error('❌ Failed to get all category IDs');
            showNotification('Không thể mở rộng tất cả danh mục', 'error');
        }
    }).fail(function () {
        showNotification('Lỗi kết nối khi mở rộng danh mục', 'error');
    });
}

function collapseAllCategories() {
    console.log('📁 Collapsing all categories...');

    expandedState.clear();
    currentState.expandedIds = [];
    loadCategoryTree();
}

function refreshCategories() {
    console.log('🔄 Refreshing categories - clearing all cache...');

    // Clear all caches
    categoryCache.clear();
    expandedState.clear();
    currentState.expandedIds = [];

    // Show loading and reload
    showLoading('#categoryTreeContainer');
    loadCategoryTree();

    showNotification('Đã làm mới danh mục', 'success');
}

function loadCategoryTree() {
    console.log('🌳 Loading category tree...');
    showLoading('#categoryTreeContainer');

    $.post('/Home/GetCategoryTreePartial', {
        selectedCategoryId: currentState.categoryId,
        expandedIds: currentState.expandedIds,
        searchTerm: currentState.searchTerm,
        categorySearch: currentState.categorySearch
    }, function (html) {
        $('#categoryTreeContainer').html(html);
        console.log('✅ Category tree loaded');
    }).fail(function (xhr, status, error) {
        console.error('❌ Failed to load category tree:', error);
        $('#categoryTreeContainer').html(`
            <div class="alert alert-danger">
                <h6>Lỗi tải danh mục</h6>
                <p class="mb-2">Không thể tải danh sách danh mục: ${error}</p>
                <button class="btn btn-sm btn-outline-danger" onclick="refreshCategories()">
                    <i class="fas fa-redo mr-1"></i>Thử lại
                </button>
            </div>
        `);
    });
}

// ===== SEARCH FUNCTIONS =====

function handleCategorySearch() {
    const term = $('#categorySearchInput').val().trim();

    clearTimeout(searchTimeouts.category);
    searchTimeouts.category = setTimeout(function () {
        if (currentState.categorySearch !== term) {
            currentState.categorySearch = term;
            loadCategoryTree();
        }
    }, 500);
}

function performCategorySearch() {
    const term = $('#categorySearchInput').val().trim();
    console.log('🔍 Performing category search:', term);

    currentState.categorySearch = term;
    loadCategoryTree();
}

function clearCategorySearch() {
    console.log('🧹 Clearing category search');
    $('#categorySearchInput').val('');
    currentState.categorySearch = '';
    loadCategoryTree();
}

function handleNewsSearch() {
    const term = $('#newsSearchInput').val().trim();

    clearTimeout(searchTimeouts.news);
    searchTimeouts.news = setTimeout(function () {
        if (term !== currentState.searchTerm) {
            performNewsSearch();
        }
    }, 800);
}

function performNewsSearch() {
    const term = $('#newsSearchInput').val().trim();
    console.log('🔍 Performing news search:', term);

    currentState.searchTerm = term;
    currentState.categoryId = null; // Clear category when searching
    currentState.page = 1;

    updateContentTitle(term ? `Kết quả tìm kiếm: "${term}"` : 'Tin tức mới nhất');
    loadNews();
    loadCategoryTree(); // Update category selection
    updateUrl();
}

function clearNewsSearch() {
    console.log('🧹 Clearing news search');
    $('#newsSearchInput').val('');
    currentState.searchTerm = '';
    currentState.page = 1;

    updateContentTitle('Tin tức mới nhất');
    loadNews();
    updateUrl();
}

// ===== NEWS FUNCTIONS =====

function loadNews() {
    console.log('📰 Loading news with state:', currentState);
    showLoading('#newsContainer');

    $.post('/Home/GetNewsPartial', {
        categoryId: currentState.categoryId,
        searchTerm: currentState.searchTerm,
        page: currentState.page,
        sortBy: currentState.sortBy,
        pageSize: 12
    }, function (html) {
        $('#newsContainer').html(html);

        // Update total count in header
        updateNewsCount();

        console.log('✅ News loaded for page:', currentState.page);
        updateUrl();
    }).fail(function (xhr, status, error) {
        console.error('❌ Failed to load news:', error);
        $('#newsContainer').html(`
            <div class="alert alert-danger">
                <h6>Lỗi tải tin tức</h6>
                <p class="mb-2">Không thể tải tin tức: ${error}</p>
                <button class="btn btn-sm btn-outline-danger" onclick="loadNews()">
                    <i class="fas fa-redo mr-1"></i>Thử lại
                </button>
            </div>
        `);
    });
}

function handlePaginationClick(e) {
    e.preventDefault();
    const page = parseInt($(this).data('page'));

    if (page && page !== currentState.page) {
        console.log('📄 Changing to page:', page);
        currentState.page = page;
        loadNews();

        // Scroll to top of news container
        $('#newsContainer')[0].scrollIntoView({ behavior: 'smooth' });
    }
}

function updateNewsCount() {
    // Extract count from loaded content if available
    const totalCountElement = $('#totalNewsCount');
    if (totalCountElement.length) {
        const count = totalCountElement.text();
        $('#totalNewsCount').text(count);
    }
}

// ===== UTILITY FUNCTIONS =====

function updateContentTitle(title) {
    $('#contentTitleText').text(title);
}

function showLoading(selector) {
    $(selector).html(`
        <div class="loading-container text-center py-4">
            <div class="spinner-border text-primary mb-3" role="status">
                <span class="sr-only">Đang tải...</span>
            </div>
            <p class="mb-0 text-muted">Đang tải dữ liệu...</p>
        </div>
    `);
}

function updateUrl() {
    const params = new URLSearchParams();

    if (currentState.categoryId) {
        params.set('categoryId', currentState.categoryId);
    }
    if (currentState.searchTerm) {
        params.set('searchTerm', currentState.searchTerm);
    }
    if (currentState.categorySearch) {
        params.set('categorySearch', currentState.categorySearch);
    }
    if (currentState.page > 1) {
        params.set('page', currentState.page);
    }
    if (currentState.sortBy !== 'newest') {
        params.set('sortBy', currentState.sortBy);
    }

    // Add expanded categories
    currentState.expandedIds.forEach(id => {
        params.append('expanded', id);
    });

    const newUrl = window.location.pathname + (params.toString() ? '?' + params.toString() : '');

    try {
        history.replaceState(null, null, newUrl);
    } catch (e) {
        console.warn('Cannot update URL:', e);
    }
}

function showNotification(message, type = 'success') {
    const alertClass = 'alert-' + type;
    const iconClass = type === 'success' ? 'fa-check-circle' :
        type === 'error' || type === 'danger' ? 'fa-exclamation-triangle' :
            type === 'warning' ? 'fa-exclamation-circle' : 'fa-info-circle';

    const notification = $(`
        <div class="alert ${alertClass} alert-dismissible fade show notification-toast" 
             role="alert" style="position: fixed; top: 20px; right: 20px; z-index: 9999; min-width: 300px;">
            <i class="fas ${iconClass} mr-2"></i>
            ${message}
            <button type="button" class="close" data-dismiss="alert">
                <span>&times;</span>
            </button>
        </div>
    `);

    $('body').append(notification);

    // Auto dismiss
    setTimeout(function () {
        notification.fadeOut(function () {
            $(this).remove();
        });
    }, 5000);
}

// ===== HELPER FUNCTIONS =====

function getCategoryLinkClass(level) {
    switch (level) {
        case 0: return 'category-link';
        case 1: return 'subcategory-link';
        case 2: return 'sub-subcategory-link';
        case 3: return 'level-4-link';
        default: return 'level-5-link';
    }
}

function getCategoryIconClass(level) {
    switch (level) {
        case 0: return 'fas fa-folder';
        case 1: return 'fas fa-folder-open';
        case 2: return 'far fa-folder';
        case 3: return 'fas fa-file-alt';
        default: return 'fas fa-circle';
    }
}

function escapeHtml(text) {
    if (!text) return '';
    var div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// ===== DEBUG & MAINTENANCE =====

function debugCache() {
    console.log('📊 CACHE DEBUG INFO:');
    console.log('Category cache size:', categoryCache.size);
    console.log('Expanded state size:', expandedState.size);
    console.log('Cached categories:', Array.from(categoryCache.keys()));
    console.log('Expanded categories:', Array.from(expandedState));
    console.log('Current state:', currentState);

    return {
        cacheSize: categoryCache.size,
        expandedCount: expandedState.size,
        cachedIds: Array.from(categoryCache.keys()),
        expandedIds: Array.from(expandedState),
        currentState: currentState
    };
}

function clearAllCache() {
    console.log('🧹 Clearing all cache...');
    categoryCache.clear();
    expandedState.clear();
    currentState.expandedIds = [];
    showNotification('Cache đã được xóa', 'info');
}

// ===== AUTO-MAINTENANCE =====

// Auto-clear cache every 15 minutes to prevent memory issues
setInterval(function () {
    if (categoryCache.size > 0) {
        console.log('🕒 Auto-maintenance: Clearing old cache');
        categoryCache.clear();
        showNotification('Cache đã được làm mới tự động', 'info');
    }
}, 15 * 60 * 1000);

// ===== BROWSER BACK/FORWARD SUPPORT =====
window.addEventListener('popstate', function (e) {
    console.log('🔙 Browser back/forward detected');
    location.reload(); // Simple approach for back/forward
});

// ===== KEYBOARD SHORTCUTS =====
$(document).keydown(function (e) {
    // Ctrl+R: Refresh categories
    if (e.ctrlKey && e.keyCode === 82) {
        e.preventDefault();
        refreshCategories();
    }

    // Ctrl+E: Expand all
    if (e.ctrlKey && e.keyCode === 69) {
        e.preventDefault();
        expandAllCategories();
    }

    // Ctrl+Q: Collapse all  
    if (e.ctrlKey && e.keyCode === 81) {
        e.preventDefault();
        collapseAllCategories();
    }
});


// THAY THẾ JavaScript search trong home-page.js
var categorySearchTimeout;
var isSearchMode = true; // Default to search mode

$(document).ready(function () {
    initializeCategorySearch();
    initializeTreeBrowse();
});

function initializeCategorySearch() {
    // Real-time search với debouncing
    $('#categorySearchInput').on('input', function () {
        clearTimeout(categorySearchTimeout);
        var term = $(this).val().trim();

        if (term.length < 2) {
            showSearchPlaceholder();
            return;
        }

        // Debounce 300ms
        categorySearchTimeout = setTimeout(function () {
            performCategorySearch(term);
        }, 300);
    });

    $('#clearCategorySearch').click(function () {
        $('#categorySearchInput').val('');
        showSearchPlaceholder();
    });
}

function performCategorySearch(term) {
    showSearchLoading();

    $.ajax({
        url: '/Category/SearchCategories',
        type: 'GET',
        data: { term: term, pageSize: 50 },
        dataType: 'json',
        timeout: 10000,
        success: function (data) {
            if (data.success && data.categories.length > 0) {
                displaySearchResults(data.categories, data.hasMore);
            } else {
                showNoSearchResults(term);
            }
        },
        error: function () {
            showSearchError();
        }
    });
}

function displaySearchResults(categories, hasMore) {
    var html = '<div class="search-results-list">';

    // Results header
    html += '<div class="results-header mb-2">';
    html += '<small class="text-muted">Tìm thấy ' + categories.length + ' kết quả';
    if (hasMore) html += ' (có thêm kết quả khác)';
    html += '</small>';
    html += '</div>';

    // Category results
    categories.forEach(function (cat) {
        html += '<div class="search-result-item" data-category-id="' + cat.Id + '">';
        html += '<div class="category-item-content">';
        html += '<div class="category-main">';
        html += '<strong class="category-name">' + escapeHtml(cat.Name) + '</strong>';
        if (cat.Path && cat.Path !== cat.Name) {
            html += '<br><small class="category-path text-muted">' + escapeHtml(cat.Path) + '</small>';
        }
        html += '</div>';
        html += '<div class="category-stats">';
        html += '<span class="badge badge-primary mr-1">' + cat.NewsCount + ' tin</span>';
        if (cat.HasChildren) {
            html += '<span class="badge badge-secondary">Có danh mục con</span>';
        }
        html += '</div>';
        html += '</div>';
        html += '<button type="button" class="btn btn-sm btn-outline-primary select-category-btn">';
        html += '<i class="fas fa-check mr-1"></i>Chọn';
        html += '</button>';
        html += '</div>';
    });

    if (hasMore) {
        html += '<div class="text-center mt-2">';
        html += '<small class="text-muted">Nhập thêm ký tự để tìm chính xác hơn</small>';
        html += '</div>';
    }

    html += '</div>';
    $('#categorySearchResults').html(html);

    // Attach events
    $('.select-category-btn').click(function () {
        var item = $(this).closest('.search-result-item');
        var categoryId = item.attr('data-category-id');
        var categoryName = item.find('.category-name').text();
        selectCategory(categoryId, categoryName);
    });
}

function showSearchLoading() {
    $('#categorySearchResults').html(`
        <div class="text-center py-3">
            <div class="spinner-border spinner-border-sm text-primary mr-2"></div>
            <span>Đang tìm kiếm...</span>
        </div>
    `);
}

function showSearchPlaceholder() {
    $('#categorySearchResults').html(`
        <div class="text-center py-4 text-muted">
            <i class="fas fa-search fa-2x mb-2"></i>
            <p>Nhập từ khóa để tìm kiếm danh mục</p>
            <small>Hỗ trợ tìm kiếm trong 45,000+ danh mục</small>
        </div>
    `);
}

function showNoSearchResults(term) {
    $('#categorySearchResults').html(`
        <div class="text-center py-4">
            <i class="fas fa-search fa-2x text-muted mb-2"></i>
            <p class="text-muted mb-2">Không tìm thấy danh mục cho "<strong>${escapeHtml(term)}</strong>"</p>
            <small class="text-muted">Thử từ khóa khác hoặc kiểm tra chính tả</small>
        </div>
    `);
}

// Tree browse (lazy loading)
function initializeTreeBrowse() {
    $('#toggleTreeBrowse').click(function () {
        var container = $('#categoryTreeContainer');
        if (container.is(':visible')) {
            container.slideUp();
            $(this).html('<i class="fas fa-tree mr-1"></i>Duyệt theo cây thư mục');
        } else {
            container.slideDown();
            $(this).html('<i class="fas fa-tree mr-1"></i>Ẩn cây thư mục');
            if (!container.hasClass('loaded')) {
                loadRootCategoriesPaged();
                container.addClass('loaded');
            }
        }
    });
}

function loadRootCategoriesPaged(page = 1) {
    $.ajax({
        url: '/Category/GetRootCategoriesPaged',
        type: 'GET',
        data: { page: page, pageSize: 30 },
        success: function (data) {
            if (data.success) {
                displayTreeCategories(data.categories, data.hasMore, page);
            }
        },
        error: function () {
            $('#categoryTreeContainer').html('<div class="alert alert-danger">Lỗi tải danh mục</div>');
        }
    });
}

function displayTreeCategories(categories, hasMore, page) {
    var html = '<div class="tree-categories">';

    categories.forEach(function (cat) {
        html += '<div class="tree-category-item" data-category-id="' + cat.Id + '">';
        html += '<div class="d-flex justify-content-between align-items-center py-1">';
        html += '<div class="flex-grow-1 category-selection" data-category-id="' + cat.Id + '" data-category-name="' + escapeHtml(cat.Name) + '">';
        html += '<i class="fas fa-folder mr-2"></i>' + escapeHtml(cat.Name);
        html += '<small class="text-muted ml-2">(' + cat.NewsCount + ' tin)</small>';
        html += '</div>';
        if (cat.HasChildren) {
            html += '<button type="button" class="btn btn-sm btn-outline-secondary expand-btn" data-parent-id="' + cat.Id + '">';
            html += '<i class="fas fa-plus"></i>';
            html += '</button>';
        }
        html += '</div>';
        html += '<div id="children-' + cat.Id + '" class="children-container ml-3" style="display: none;"></div>';
        html += '</div>';
    });

    if (hasMore) {
        html += '<div class="text-center mt-2">';
        html += '<button type="button" class="btn btn-sm btn-outline-primary" onclick="loadRootCategoriesPaged(' + (page + 1) + ')">';
        html += 'Xem thêm danh mục...';
        html += '</button>';
        html += '</div>';
    }

    html += '</div>';

    if (page === 1) {
        $('#categoryTreeContainer').html(html);
    } else {
        $('#categoryTreeContainer .tree-categories').append(html);
    }

    // Attach events
    attachTreeEvents();
}
// ===== EXPOSE GLOBAL FUNCTIONS =====
window.homePageFunctions = {
    debugCache,
    clearAllCache,
    refreshCategories,
    expandAllCategories,
    collapseAllCategories,
    retryLoadSubcategories,
    getCacheStats: debugCache
};

console.log('✅ Home page script fully loaded and initialized');
console.log('🔧 Debug functions available: window.homePageFunctions');