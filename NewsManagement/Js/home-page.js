/**
 * Home Page JavaScript Module
 * Quản lý tương tác trang chủ
 */

var NewsApp = (function () {
    'use strict';

    // ===== PRIVATE VARIABLES =====
    var config = {
        searchTimeout: null,
        categorySearchTimeout: null,
        loadedCategories: new Set(),
        currentMode: 'default',
        pageSize: 20,
        urls: {
            base: '',
            home: '',
            news: '',
            category: ''
        }
    };

    var cache = {
        categories: null,
        recentNews: null
    };

    var elements = {
        searchInput: null,
        contentContainer: null,
        contentHeader: null,
        contentTitle: null,
        categoriesMenu: null
    };

    // ===== PRIVATE METHODS =====

    /**
     * Khởi tạo các element DOM
     */
    function initElements() {
        elements.searchInput = document.getElementById('search-input');
        elements.contentContainer = document.getElementById('content-container');
        elements.contentHeader = document.getElementById('content-header');
        elements.contentTitle = document.getElementById('content-title');
        elements.categoriesMenu = document.getElementById('categories-menu');
    }

    /**
     * Gọi PartialView thay vì API
     */
    function loadPartialView(url, data, callback) {
        $.get(url, data, function (html) {
            if (typeof callback === 'function') {
                callback(html);
            }
        }).fail(function (xhr, status, error) {
            console.error('Error loading partial view:', error);
            if (typeof callback === 'function') {
                callback(null, error);
            }
        });
    }

    /**
     * Hiển thị loading
     */
    function showLoading() {
        if (elements.contentContainer) {
            elements.contentContainer.innerHTML =
                '<div class="loading-spinner">' +
                '<div class="spinner-border text-primary" role="status">' +
                '<span class="sr-only">Loading...</span>' +
                '</div>' +
                '<p class="mt-3">Đang tải...</p>' +
                '</div>';
        }
    }

    /**
     * Cập nhật header
     */
    function updateHeader(mode, title) {
        if (elements.contentHeader && elements.contentTitle) {
            elements.contentHeader.className = 'card-header bg-' + mode + ' text-white';
            elements.contentTitle.innerHTML = '<i class="fa fa-' + getIconForMode(mode) + ' content-header-icon"></i>' + title;
        }
    }

    /**
     * Lấy icon cho mode
     */
    function getIconForMode(mode) {
        var icons = {
            'search': 'search',
            'category': 'folder',
            'default': 'newspaper'
        };
        return icons[mode] || 'newspaper';
    }

    /**
     * Escape HTML
     */
    function escapeHtml(text) {
        if (!text) return '';
        var div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Set active category
     */
    function setActiveCategory(categoryId) {
        // Remove active state
        var links = document.querySelectorAll('.category-link, .subcategory-link, .sub-subcategory-link, .level-4-link, .level-5-link, .level-6-link, .level-7-link, .level-8-link');
        for (var i = 0; i < links.length; i++) {
            links[i].classList.remove('active');
        }

        // Add active state
        var activeLinks = document.querySelectorAll('[data-category-id="' + categoryId + '"]');
        for (var j = 0; j < activeLinks.length; j++) {
            activeLinks[j].classList.add('active');
        }
    }

    /**
     * Generate category level class
     */
    function getCategoryLevelClass(level) {
        var classes = {
            1: 'category-link',
            2: 'subcategory-link',
            3: 'sub-subcategory-link',
            4: 'level-4-link',
            5: 'level-5-link',
            6: 'level-6-link',
            7: 'level-7-link',
            8: 'level-8-link'
        };
        return classes[level] || 'level-8-link';
    }

    // ===== CATEGORY METHODS =====

    /**
     * Load categories với PartialView
     */
    function loadCategories() {
        loadPartialView('/Home/GetCategoriesPartial', {}, function (html, error) {
            if (error) {
                showErrorCategories();
                return;
            }

            if (html && elements.categoriesMenu) {
                elements.categoriesMenu.innerHTML = html;
                attachCategoryEventListeners();
            } else {
                showEmptyCategories();
            }
        });
    }

    /**
     * Load subcategories
     */
    function loadSubcategories(parentId, level) {
        var container = document.getElementById('subcategories-' + parentId);
        var toggle = document.getElementById('toggle-' + parentId);

        if (!container) return;

        container.innerHTML = '<div class="text-center p-2"><i class="fa fa-spinner fa-spin"></i> Đang tải...</div>';
        container.classList.add('expanded');
        if (toggle) toggle.classList.add('expanded');

        loadPartialView('/Home/GetSubcategoriesPartial', { parentId: parentId }, function (html, error) {
            if (error) {
                container.innerHTML = '<div class="text-center p-2 text-danger">Lỗi tải dữ liệu</div>';
                return;
            }

            if (html) {
                container.innerHTML = html;
                config.loadedCategories.add(parentId);
                attachSubcategoryEventListeners(container);
            } else {
                container.innerHTML = '<div class="text-center p-2 text-muted">Không có danh mục con</div>';
            }
        });
    }

    /**
     * Toggle category
     */
    function toggleCategory(categoryId, level) {
        var container = document.getElementById('subcategories-' + categoryId);
        var toggle = document.getElementById('toggle-' + categoryId);

        if (!container || !toggle) return;

        var isExpanded = container.classList.contains('expanded');

        if (isExpanded) {
            container.classList.remove('expanded');
            toggle.classList.remove('expanded');
        } else {
            if (!config.loadedCategories.has(categoryId)) {
                loadSubcategories(categoryId, level + 1);
            } else {
                container.classList.add('expanded');
                toggle.classList.add('expanded');
            }
        }
    }

    /**
     * Load news by category
     */
    function loadNewsByCategory(categoryId, categoryName) {
        config.currentMode = 'category';
        updateHeader('category', 'Tin tức trong danh mục: ' + categoryName);
        setActiveCategory(categoryId);
        showLoading();

        loadPartialView('/Home/GetNewsByCategoryPartial', {
            categoryId: categoryId,
            pageSize: config.pageSize
        }, function (html, error) {
            if (error) {
                showCategoryError(categoryName);
                return;
            }

            if (html) {
                elements.contentContainer.innerHTML = html;
                // Add back button
                var backButton = '<div class="text-center mt-4">' +
                    '<button onclick="NewsApp.clearSearch()" class="btn btn-outline-secondary">' +
                    '<i class="fa fa-arrow-left mr-1"></i> Quay lại trang chủ' +
                    '</button></div>';
                elements.contentContainer.innerHTML += backButton;
            } else {
                showNoCategoryNews(categoryName);
            }
        });
    }

    // ===== NEWS METHODS =====

    /**
     * Load recent news
     */
    function loadRecentNews() {
        loadPartialView('/Home/GetRecentNewsPartial', { count: config.pageSize }, function (html, error) {
            if (error) {
                showErrorNews('Không thể tải tin tức');
                return;
            }

            if (html) {
                elements.contentContainer.innerHTML = html;
            } else {
                showEmptyNews();
            }
        });
    }

    // ===== SEARCH METHODS =====

    /**
     * Perform search
     */
    function performSearch() {
        var query = elements.searchInput ? elements.searchInput.value.trim() : '';

        if (query.length < 2) {
            alert('Vui lòng nhập ít nhất 2 ký tự để tìm kiếm');
            return;
        }

        config.currentMode = 'search';
        updateHeader('search', 'Kết quả tìm kiếm: "' + query + '"');
        showLoading();

        loadPartialView('/Home/QuickSearchPartial', {
            term: query,
            maxResults: config.pageSize
        }, function (html, error) {
            if (error) {
                showSearchError('Lỗi tìm kiếm');
                return;
            }

            if (html) {
                elements.contentContainer.innerHTML = html;
            } else {
                showNoSearchResults(query);
            }
        });
    }

    /**
     * Clear search
     */
    function clearSearch() {
        if (elements.searchInput) {
            elements.searchInput.value = '';
        }
        config.currentMode = 'default';
        updateHeader('default', 'Tin tức mới nhất');
        loadRecentNews();
    }

    /**
     * Handle search input
     */
    function handleSearchInput(event) {
        clearTimeout(config.searchTimeout);

        if (event.key === 'Enter') {
            performSearch();
            return;
        }

        config.searchTimeout = setTimeout(function () {
            var query = elements.searchInput.value.trim();
            if (query.length >= 2) {
                performSearch();
            } else if (query.length === 0) {
                clearSearch();
            }
        }, 500);
    }

    // ===== EVENT LISTENERS =====

    /**
     * Attach category event listeners
     */
    function attachCategoryEventListeners() {
        // Category click events
        var categoryLinks = document.querySelectorAll('.category-link, .subcategory-link, .sub-subcategory-link, .level-4-link, .level-5-link, .level-6-link, .level-7-link, .level-8-link');
        for (var i = 0; i < categoryLinks.length; i++) {
            categoryLinks[i].addEventListener('click', function (e) {
                if (e.target.closest('.category-toggle-btn')) {
                    return;
                }

                var categoryId = this.getAttribute('data-category-id');
                var categoryName = this.getAttribute('data-category-name');

                if (categoryId && categoryName) {
                    loadNewsByCategory(parseInt(categoryId), categoryName);
                }
            });
        }

        // Toggle button events
        var toggleButtons = document.querySelectorAll('.category-toggle-btn');
        for (var j = 0; j < toggleButtons.length; j++) {
            toggleButtons[j].addEventListener('click', function (e) {
                e.stopPropagation();
                var categoryId = this.getAttribute('data-category-id');
                var level = parseInt(this.getAttribute('data-level')) || 1;

                if (categoryId) {
                    toggleCategory(parseInt(categoryId), level);
                }
            });
        }
    }

    /**
     * Attach subcategory event listeners
     */
    function attachSubcategoryEventListeners(container) {
        if (!container) return;

        var subCategoryLinks = container.querySelectorAll('.category-link, .subcategory-link, .sub-subcategory-link, .level-4-link, .level-5-link, .level-6-link, .level-7-link, .level-8-link');
        for (var i = 0; i < subCategoryLinks.length; i++) {
            subCategoryLinks[i].addEventListener('click', function (e) {
                if (e.target.closest('.category-toggle-btn')) {
                    return;
                }

                var categoryId = this.getAttribute('data-category-id');
                var categoryName = this.getAttribute('data-category-name');

                if (categoryId && categoryName) {
                    loadNewsByCategory(parseInt(categoryId), categoryName);
                }
            });
        }

        var subToggleButtons = container.querySelectorAll('.category-toggle-btn');
        for (var j = 0; j < subToggleButtons.length; j++) {
            subToggleButtons[j].addEventListener('click', function (e) {
                e.stopPropagation();
                var categoryId = this.getAttribute('data-category-id');
                var level = parseInt(this.getAttribute('data-level')) || 1;

                if (categoryId) {
                    toggleCategory(parseInt(categoryId), level);
                }
            });
        }
    }

    // ===== ERROR STATES =====

    function showEmptyNews() {
        elements.contentContainer.innerHTML =
            '<div class="alert alert-info text-center">' +
            '<h4><i class="fa fa-info-circle"></i> Chưa có tin tức</h4>' +
            '<p>Hiện tại chưa có tin tức nào trong hệ thống.</p>' +
            '<a href="/News/Create" class="btn btn-primary">' +
            '<i class="fa fa-plus"></i> Thêm tin tức đầu tiên</a>' +
            '</div>';
    }

    function showErrorNews(message) {
        elements.contentContainer.innerHTML =
            '<div class="alert alert-danger text-center">' +
            '<h4><i class="fa fa-exclamation-triangle"></i> Lỗi</h4>' +
            '<p>' + escapeHtml(message) + '</p>' +
            '<button onclick="NewsApp.loadRecentNews()" class="btn btn-secondary">' +
            '<i class="fa fa-refresh"></i> Thử lại</button>' +
            '</div>';
    }

    function showNoSearchResults(query) {
        elements.contentContainer.innerHTML =
            '<div class="alert alert-warning text-center">' +
            '<h4><i class="fa fa-search"></i> Không tìm thấy kết quả</h4>' +
            '<p>Không có tin tức nào chứa từ khóa: "<strong>' + escapeHtml(query) + '</strong>"</p>' +
            '<button onclick="NewsApp.clearSearch()" class="btn btn-primary">Về trang chủ</button>' +
            '</div>';
    }

    function showSearchError(message) {
        elements.contentContainer.innerHTML =
            '<div class="alert alert-danger text-center">' +
            '<h4><i class="fa fa-exclamation-triangle"></i> Lỗi tìm kiếm</h4>' +
            '<p>' + escapeHtml(message) + '</p>' +
            '<button onclick="NewsApp.clearSearch()" class="btn btn-primary">Về trang chủ</button>' +
            '</div>';
    }

    function showNoCategoryNews(categoryName) {
        elements.contentContainer.innerHTML =
            '<div class="alert alert-info text-center">' +
            '<h4><i class="fa fa-folder-open"></i> Danh mục trống</h4>' +
            '<p>Danh mục "<strong>' + escapeHtml(categoryName) + '</strong>" chưa có tin tức nào.</p>' +
            '<button onclick="NewsApp.clearSearch()" class="btn btn-primary">Về trang chủ</button>' +
            '</div>';
    }

    function showCategoryError(categoryName) {
        elements.contentContainer.innerHTML =
            '<div class="alert alert-danger text-center">' +
            '<h4><i class="fa fa-exclamation-triangle"></i> Lỗi tải tin tức</h4>' +
            '<p>Không thể tải tin tức từ danh mục: "<strong>' + escapeHtml(categoryName) + '</strong>"</p>' +
            '<button onclick="NewsApp.clearSearch()" class="btn btn-primary">Về trang chủ</button>' +
            '</div>';
    }

    function showEmptyCategories() {
        if (elements.categoriesMenu) {
            elements.categoriesMenu.innerHTML =
                '<div class="text-center p-3">' +
                '<p class="text-muted mb-2">Chưa có danh mục nào</p>' +
                '<a href="/Category/Create" class="btn btn-sm btn-success">' +
                '<i class="fa fa-plus"></i> Thêm danh mục</a>' +
                '</div>';
        }
    }

    function showErrorCategories() {
        if (elements.categoriesMenu) {
            elements.categoriesMenu.innerHTML =
                '<div class="text-center p-3">' +
                '<p class="text-muted mb-2">Không thể tải danh mục</p>' +
                '<button onclick="NewsApp.loadCategories()" class="btn btn-sm btn-secondary">' +
                '<i class="fa fa-refresh"></i> Thử lại</button>' +
                '</div>';
        }
    }

    // ===== INITIALIZATION =====

    /**
     * Initialize the app
     */
    function init() {
        // Wait for DOM to be ready
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', function () {
                initElements();
                setupEventListeners();
                loadInitialData();
            });
        } else {
            initElements();
            setupEventListeners();
            loadInitialData();
        }
    }

    /**
     * Setup event listeners
     */
    function setupEventListeners() {
        if (elements.searchInput) {
            elements.searchInput.addEventListener('keyup', handleSearchInput);
        }
    }

    /**
     * Load initial data
     */
    function loadInitialData() {
        loadRecentNews();
        loadCategories();
    }

    // ===== PUBLIC API =====
    return {
        init: init,
        loadRecentNews: loadRecentNews,
        loadCategories: loadCategories,
        performSearch: performSearch,
        clearSearch: clearSearch,
        loadNewsByCategory: loadNewsByCategory,
        setUrls: function (urls) {
            config.urls = urls;
        }
    };

})();

// Initialize when script loads
NewsApp.init();