var currentMode = 'default';
var searchTimeout;
var loadedCategories = new Set();
var categorySearchTimeout;

// Base URLs - sẽ được set từ HTML
var baseUrl = '';
var homeUrl = '';
var newsUrl = '';

document.addEventListener('DOMContentLoaded', function () {
    loadRecentNews();
    loadCategories();
});

function setUrls(base, home, news) {
    baseUrl = base;
    homeUrl = home;
    newsUrl = news;
}

function handleSearch(event) {
    clearTimeout(searchTimeout);

    if (event.key === 'Enter') {
        performSearch();
        return;
    }

    searchTimeout = setTimeout(function () {
        var query = document.getElementById('search-input').value.trim();
        if (query.length >= 2) {
            performSearch();
        } else if (query.length === 0) {
            clearSearch();
        }
    }, 500);
}

function performSearch() {
    var query = document.getElementById('search-input').value.trim();

    if (query.length < 2) {
        alert('Vui lòng nhập ít nhất 2 ký tự để tìm kiếm');
        return;
    }

    currentMode = 'search';
    updateHeader('search', 'Kết quả tìm kiếm: "' + query + '"');
    showLoading();

    var xhr = new XMLHttpRequest();
    xhr.open('GET', homeUrl + '/QuickSearch?term=' + encodeURIComponent(query) + '&maxResults=20', true);

    xhr.onreadystatechange = function () {
        if (xhr.readyState === 4) {
            if (xhr.status === 200) {
                try {
                    var data = JSON.parse(xhr.responseText);
                    if (data.success && data.results && data.results.length > 0) {
                        displaySearchResults(data.results, query);
                    } else {
                        showNoSearchResults(query);
                    }
                } catch (e) {
                    showSearchError('Lỗi xử lý kết quả tìm kiếm');
                }
            } else {
                showSearchError('Không thể thực hiện tìm kiếm');
            }
        }
    };

    xhr.send();
}

function clearSearch() {
    document.getElementById('search-input').value = '';
    currentMode = 'default';
    updateHeader('default', '20 Tin tức mới nhất');
    loadRecentNews();
}

function loadNewsByCategory(categoryId, categoryName) {
    currentMode = 'category';
    updateHeader('category', 'Tin tức trong danh mục: ' + categoryName);
    showLoading();

    var xhr = new XMLHttpRequest();
    xhr.open('GET', newsUrl + '/GetNewsByCategory?categoryId=' + categoryId + '&pageSize=20', true);

    xhr.onreadystatechange = function () {
        if (xhr.readyState === 4) {
            if (xhr.status === 200) {
                try {
                    var data = JSON.parse(xhr.responseText);
                    if (data.success && data.data && data.data.length > 0) {
                        displayCategoryNews(data.data, categoryName);
                    } else {
                        showNoCategoryNews(categoryName);
                    }
                } catch (e) {
                    showCategoryError(categoryName);
                }
            } else {
                showCategoryError(categoryName);
            }
        }
    };

    xhr.send();
}

function loadCategoryNews(categoryId, categoryName) {
    setActiveCategory(categoryId);
    loadNewsByCategory(categoryId, categoryName);
}

function loadRecentNews() {
    var xhr = new XMLHttpRequest();
    xhr.open('GET', homeUrl + '/GetRecentNews?count=20', true);

    xhr.onreadystatechange = function () {
        if (xhr.readyState === 4) {
            if (xhr.status === 200) {
                try {
                    var data = JSON.parse(xhr.responseText);
                    if (data.success && data.news && data.news.length > 0) {
                        displayNews(data.news);
                    } else {
                        showEmptyNews();
                    }
                } catch (e) {
                    showErrorNews('Lỗi xử lý dữ liệu tin tức');
                }
            } else {
                showErrorNews('Không thể tải tin tức');
            }
        }
    };

    xhr.send();
}

function loadCategories() {
    var xhr = new XMLHttpRequest();
    xhr.open('GET', homeUrl + '/GetCategoriesTree', true);

    xhr.onreadystatechange = function () {
        if (xhr.readyState === 4) {
            if (xhr.status === 200) {
                try {
                    var data = JSON.parse(xhr.responseText);
                    if (data.success && data.categories) {
                        displayCategoriesOptimized(data.categories);
                    } else {
                        showEmptyCategories();
                    }
                } catch (e) {
                    showErrorCategories();
                }
            } else {
                showErrorCategories();
            }
        }
    };

    xhr.send();
}

function displayCategoriesOptimized(categories) {
    var html = '<div class="categories-menu">';

    html += '<div class="category-search-box">';
    html += '<input type="text" id="category-search" class="form-control form-control-sm" ';
    html += 'placeholder="Tìm danh mục..." onkeyup="searchCategories(event)">';
    html += '</div>';

    for (var i = 0; i < categories.length; i++) {
        var category = categories[i];
        html += generateCategoryItem(category, 1);
    }

    html += '</div>';
    document.getElementById('categories-menu').innerHTML = html;
}

function generateCategoryItem(category, level) {
    var hasChildren = category.HasChildren || (category.Children && category.Children.length > 0);
    var levelClass = getCategoryLevelClass(level);

    var html = '<div class="category-item" data-category-id="' + category.Id + '">';

    html += '<div class="' + levelClass + '" onclick="loadCategoryNews(' + category.Id + ', \'' + escapeHtml(category.Name) + '\')" data-category-id="' + category.Id + '">';
    html += '<div class="category-content">';
    html += '<i class="fa fa-' + (level === 1 ? 'folder' : level === 2 ? 'folder-o' : 'file-o') + ' text-primary"></i>';
    html += '<span>' + escapeHtml(category.Name) + '</span>';
    html += '</div>';
    html += '<div class="category-meta">';
    html += '<span class="news-count">' + category.NewsCount + '</span>';

    if (hasChildren) {
        html += '<button class="category-toggle-btn" onclick="event.stopPropagation(); toggleCategoryLazy(' + category.Id + ', ' + level + ')" id="toggle-' + category.Id + '">';
        html += '<i class="fa fa-chevron-right"></i>';
        html += '</button>';
    }
    html += '</div>';
    html += '</div>';

    html += '<div class="subcategories-container" id="subcategories-' + category.Id + '">';

    if (category.Children && category.Children.length > 0) {
        for (var j = 0; j < category.Children.length; j++) {
            html += generateCategoryItem(category.Children[j], level + 1);
        }
        loadedCategories.add(category.Id);
    }

    html += '</div>';
    html += '</div>';

    return html;
}

function toggleCategoryLazy(categoryId, level) {
    var container = document.getElementById('subcategories-' + categoryId);
    var toggle = document.getElementById('toggle-' + categoryId);

    if (!container || !toggle) return;

    var isExpanded = container.classList.contains('expanded');

    if (isExpanded) {
        container.classList.remove('expanded');
        toggle.classList.remove('expanded');
    } else {
        if (!loadedCategories.has(categoryId)) {
            loadSubcategories(categoryId, level + 1);
        } else {
            container.classList.add('expanded');
            toggle.classList.add('expanded');
        }
    }
}

function loadSubcategories(parentId, level) {
    var container = document.getElementById('subcategories-' + parentId);
    var toggle = document.getElementById('toggle-' + parentId);

    container.innerHTML = '<div class="text-center p-2"><i class="fa fa-spinner fa-spin"></i> Đang tải...</div>';
    container.classList.add('expanded');
    toggle.classList.add('expanded');

    var xhr = new XMLHttpRequest();
    xhr.open('GET', homeUrl + '/GetSubcategories?parentId=' + parentId, true);

    xhr.onreadystatechange = function () {
        if (xhr.readyState === 4) {
            if (xhr.status === 200) {
                try {
                    var data = JSON.parse(xhr.responseText);
                    if (data.success && data.subcategories) {
                        var html = '';
                        for (var i = 0; i < data.subcategories.length; i++) {
                            html += generateCategoryItem(data.subcategories[i], level);
                        }
                        container.innerHTML = html;
                        loadedCategories.add(parentId);
                    } else {
                        container.innerHTML = '<div class="text-center p-2 text-muted">Không có danh mục con</div>';
                    }
                } catch (e) {
                    container.innerHTML = '<div class="text-center p-2 text-danger">Lỗi tải dữ liệu</div>';
                }
            } else {
                container.innerHTML = '<div class="text-center p-2 text-danger">Không thể tải danh mục con</div>';
            }
        }
    };

    xhr.send();
}

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

function setActiveCategory(categoryId) {
    var allLinks = document.querySelectorAll('.category-link, .subcategory-link, .sub-subcategory-link, .level-4-link, .level-5-link, .level-6-link, .level-7-link, .level-8-link');
    for (var i = 0; i < allLinks.length; i++) {
        allLinks[i].classList.remove('active');
    }

    var selectedLink = document.querySelector('[data-category-id="' + categoryId + '"]');
    if (selectedLink) {
        selectedLink.classList.add('active');
    }
}

function updateHeader(mode, title) {
    var header = document.getElementById('content-header');
    var titleElement = document.getElementById('content-title');

    header.className = 'card-header bg-' + mode;
    titleElement.innerHTML = '<i class="fa fa-' + getIconForMode(mode) + '"></i> ' + title;
}

function getIconForMode(mode) {
    switch (mode) {
        case 'search': return 'search';
        case 'category': return 'folder';
        default: return 'newspaper';
    }
}

function showLoading() {
    document.getElementById('content-container').innerHTML =
        '<div class="text-center">' +
        '<div class="spinner-border text-primary" role="status" style="width: 3rem; height: 3rem;">' +
        '<span class="sr-only">Loading...</span>' +
        '</div>' +
        '<p class="mt-3">Đang tải...</p>' +
        '</div>';
}

function displayNews(newsArray) {
    var html = '<div class="row">';

    for (var i = 0; i < newsArray.length; i++) {
        var news = newsArray[i];
        html += generateNewsCard(news);
    }

    html += '</div>';

    if (currentMode === 'default') {
        html += '<div class="text-center mt-4">';
        html += '<a href="' + newsUrl + '" class="btn btn-primary btn-lg">';
        html += '<i class="fa fa-eye"></i> Xem tất cả tin tức';
        html += '</a>';
        html += '</div>';
    }

    document.getElementById('content-container').innerHTML = html;
}

function displayCategoryNews(newsArray, categoryName) {
    var html = '<div class="alert alert-success">';
    html += '<strong>Tìm thấy ' + newsArray.length + ' tin tức trong danh mục: "' + escapeHtml(categoryName) + '"</strong>';
    html += '</div>';

    html += '<div class="row">';

    for (var i = 0; i < newsArray.length; i++) {
        var news = newsArray[i];
        html += generateNewsCard(news);
    }

    html += '</div>';

    document.getElementById('content-container').innerHTML = html;
}

function displaySearchResults(results, query) {
    var html = '<div class="alert alert-info">';
    html += '<strong>Tìm thấy ' + results.length + ' kết quả cho từ khóa: "' + escapeHtml(query) + '"</strong>';
    html += '</div>';

    html += '<div class="row">';

    for (var i = 0; i < results.length; i++) {
        var news = results[i];
        html += generateSearchResultCard(news, query);
    }

    html += '</div>';

    document.getElementById('content-container').innerHTML = html;
}

function generateNewsCard(news) {
    var html = '<div class="col-lg-3 col-md-6 col-sm-12 mb-3">';
    html += '<div class="card news-card h-100">';
    html += '<div class="card-body d-flex flex-column">';

    html += '<h6 class="card-title">';
    html += '<a href="/News/Details/' + news.Id + '" class="news-title">' + escapeHtml(news.Title) + '</a>';
    html += '</h6>';

    if (news.Categories && news.Categories.length > 0) {
        html += '<div class="mb-2">';
        for (var j = 0; j < news.Categories.length; j++) {
            html += '<span class="badge badge-secondary category-badge">' + escapeHtml(news.Categories[j]) + '</span>';
        }
        html += '</div>';
    }

    if (news.Summary || news.summary) {
        var summary = news.Summary || news.summary;
        summary = summary.length > 100 ? summary.substring(0, 100) + '...' : summary;
        html += '<p class="card-text news-summary flex-grow-1">' + escapeHtml(summary) + '</p>';
    }

    html += '<div class="mt-auto">';
    html += '<div class="d-flex justify-content-between align-items-center">';
    html += '<small class="news-meta">';
    html += '<i class="fa fa-calendar"></i> ' + escapeHtml(news.CreatedDate || new Date().toLocaleDateString());
    html += '</small>';
    html += '<div>';
    html += '<a href="/News/Details/' + news.Id + '" class="btn btn-sm btn-outline-primary">Xem</a>';
    html += '<a href="/News/Edit/' + news.Id + '" class="btn btn-sm btn-outline-warning ml-1">Sửa</a>';
    html += '</div>';
    html += '</div>';
    html += '</div>';

    html += '</div></div></div>';
    return html;
}

function generateSearchResultCard(result, query) {
    var html = '<div class="col-lg-4 col-md-6 col-sm-12 mb-3">';
    html += '<div class="card news-card h-100">';
    html += '<div class="card-body d-flex flex-column">';

    html += '<h6 class="card-title">';
    html += '<a href="' + result.url + '" class="news-title">' + highlightSearchTerm(result.title, query) + '</a>';
    html += '</h6>';

    if (result.summary) {
        html += '<p class="card-text news-summary flex-grow-1">' + highlightSearchTerm(result.summary, query) + '</p>';
    }

    html += '<div class="mt-auto">';
    html += '<div class="text-center">';
    html += '<a href="' + result.url + '" class="btn btn-sm btn-outline-primary">Xem chi tiết</a>';
    html += '</div>';
    html += '</div>';

    html += '</div></div></div>';
    return html;
}

function highlightSearchTerm(text, term) {
    if (!text || !term) return escapeHtml(text);
    var regex = new RegExp('(' + escapeRegex(term) + ')', 'gi');
    return escapeHtml(text).replace(regex, '<span class="search-highlight">$1</span>');
}

// Error handlers
function showNoSearchResults(query) {
    document.getElementById('content-container').innerHTML =
        '<div class="alert alert-warning text-center">' +
        '<h4><i class="fa fa-search"></i> Không tìm thấy kết quả</h4>' +
        '<p>Không có tin tức nào chứa từ khóa: "<strong>' + escapeHtml(query) + '</strong>"</p>' +
        '<button onclick="clearSearch()" class="btn btn-primary">Về trang chủ</button>' +
        '</div>';
}

function showNoCategoryNews(categoryName) {
    document.getElementById('content-container').innerHTML =
        '<div class="alert alert-info text-center">' +
        '<h4><i class="fa fa-folder-open"></i> Danh mục trống</h4>' +
        '<p>Danh mục "<strong>' + escapeHtml(categoryName) + '</strong>" chưa có tin tức nào.</p>' +
        '<button onclick="clearSearch()" class="btn btn-primary">Về trang chủ</button>' +
        '</div>';
}

function showEmptyNews() {
    document.getElementById('content-container').innerHTML =
        '<div class="alert alert-info text-center">' +
        '<h4><i class="fa fa-info-circle"></i> Chưa có tin tức</h4>' +
        '<p>Hiện tại chưa có tin tức nào trong hệ thống.</p>' +
        '<a href="/News/Create" class="btn btn-primary">' +
        '<i class="fa fa-plus"></i> Thêm tin tức đầu tiên' +
        '</a>' +
        '</div>';
}

function showErrorNews(message) {
    document.getElementById('content-container').innerHTML =
        '<div class="alert alert-warning text-center">' +
        '<h4><i class="fa fa-exclamation-triangle"></i> ' + escapeHtml(message) + '</h4>' +
        '<button onclick="loadRecentNews()" class="btn btn-secondary">' +
        '<i class="fa fa-refresh"></i> Thử lại' +
        '</button>' +
        '</div>';
}

function showSearchError(message) {
    document.getElementById('content-container').innerHTML =
        '<div class="alert alert-danger text-center">' +
        '<h4><i class="fa fa-exclamation-triangle"></i> ' + escapeHtml(message) + '</h4>' +
        '<button onclick="clearSearch()" class="btn btn-primary">Về trang chủ</button>' +
        '</div>';
}

function showCategoryError(categoryName) {
    document.getElementById('content-container').innerHTML =
        '<div class="alert alert-danger text-center">' +
        '<h4><i class="fa fa-exclamation-triangle"></i> Lỗi tải tin tức</h4>' +
        '<p>Không thể tải tin tức từ danh mục: "<strong>' + escapeHtml(categoryName) + '</strong>"</p>' +
        '<button onclick="clearSearch()" class="btn btn-primary">Về trang chủ</button>' +
        '</div>';
}

function showEmptyCategories() {
    document.getElementById('categories-menu').innerHTML =
        '<div class="text-center p-3">' +
        '<p class="text-muted mb-2">Chưa có danh mục nào</p>' +
        '<a href="/Category/Create" class="btn btn-sm btn-success">' +
        '<i class="fa fa-plus"></i> Thêm danh mục' +
        '</a>' +
        '</div>';
}

function showErrorCategories() {
    document.getElementById('categories-menu').innerHTML =
        '<div class="text-center p-3">' +
        '<p class="text-muted mb-2">Không thể tải danh mục</p>' +
        '<button onclick="loadCategories()" class="btn btn-sm btn-secondary">' +
        '<i class="fa fa-refresh"></i> Thử lại' +
        '</button>' +
        '</div>';
}

function searchCategories(event) {
    // Placeholder for search functionality
    console.log('Search categories:', event.target.value);
}

function escapeHtml(text) {
    if (!text) return '';
    var map = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#039;'
    };
    return text.replace(/[&<>"']/g, function (m) { return map[m]; });
}

function escapeRegex(string) {
    return string.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}