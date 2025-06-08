   let searchTimeout;
    let currentFilter = {
        searchTerm: '@Html.Raw(filter.SearchTerm ?? "")',
    categoryId: @(filter.CategoryId?.ToString() ?? "null"),
    status: '@(filter.Status?.ToString() ?? "")',
    dateRange: '@(filter.DateRange ?? "")',
    dateFrom: '@(filter.DateFrom?.ToString("yyyy-MM-dd") ?? "")',
    dateTo: '@(filter.DateTo?.ToString("yyyy-MM-dd") ?? "")',
    sortBy: '@(filter.SortBy ?? "date_desc")',
    page: @filter.Page,
    pageSize: @filter.PageSize
};

    let selectedNewsIds = new Set();
    let isLoading = false;

    // ===== DOCUMENT READY =====
    $(document).ready(function() {
        console.log('🚀 Initializing News Management...');
    initializeEventHandlers();
    initializeQuickSearch();
    updateUrl();
});

    // ===== EVENT HANDLERS =====
    function initializeEventHandlers() {
        // Filter form changes
        $('#searchTerm').on('input', handleSearchInput);
    $('#categoryId, #status, #sortBy, #dateRange, #pageSize').change(handleFilterChange);
    $('#dateFrom, #dateTo').change(handleDateChange);

    // Keyboard shortcuts
    $(document).keydown(handleKeyboardShortcuts);

    // Form submission
    $('#filterForm').submit(function(e) {
        e.preventDefault();
    applyFilters();
    });

    // Bulk selection
    $(document).on('change', '.news-checkbox', handleNewsSelection);
    $(document).on('change', '#selectAllNews', handleSelectAll);

    // Pagination
    $(document).on('click', '.pagination-link', handlePaginationClick);

    // Quick actions
    $(document).on('click', '.quick-action', handleQuickAction);

    // View mode toggle
    $('.view-modes button').click(function() {
        $('.view-modes button').removeClass('active');
    $(this).addClass('active');
    toggleViewMode($(this).data('view'));
    });
}

    function initializeQuickSearch() {
        $('#searchTerm').on('input', function () {
            const term = $(this).val().trim();

            clearTimeout(searchTimeout);

            if (term.length >= 2) {
                searchTimeout = setTimeout(function () {
                    performQuickSearch(term);
                }, 300);
            } else {
                $('#quickSearchResults').hide();
            }
        });

    // Hide results when clicking outside
    $(document).click(function(e) {
        if (!$(e.target).closest('#searchTerm, #quickSearchResults').length) {
        $('#quickSearchResults').hide();
        }
    });
}

    // ===== SEARCH & FILTER FUNCTIONS =====
    function handleSearchInput() {
    const term = $('#searchTerm').val().trim();
    currentFilter.searchTerm = term;
    currentFilter.page = 1;

    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(function() {
        loadNewsList();
    }, 500);
}

    function handleFilterChange() {
        updateFilterFromForm();
    currentFilter.page = 1;
    loadNewsList();
}

    function handleDateChange() {
        updateFilterFromForm();

    // Clear date range if custom dates are used
    if ($('#dateFrom').val() || $('#dateTo').val()) {
        $('#dateRange').val('');
    currentFilter.dateRange = '';
    }

    currentFilter.page = 1;
    loadNewsList();
}

    function updateFilterFromForm() {
        currentFilter.searchTerm = $('#searchTerm').val().trim();
    currentFilter.categoryId = $('#categoryId').val() || null;
    currentFilter.status = $('#status').val() || null;
    currentFilter.dateRange = $('#dateRange').val() || '';
    currentFilter.dateFrom = $('#dateFrom').val() || '';
    currentFilter.dateTo = $('#dateTo').val() || '';
    currentFilter.sortBy = $('#sortBy').val() || 'date_desc';
    currentFilter.pageSize = parseInt($('#pageSize').val()) || 20;
}

    function applyFilters() {
        console.log('🔍 Applying filters:', currentFilter);
    updateFilterFromForm();
    currentFilter.page = 1;
    loadNewsList();
}

    function resetFilters() {
        console.log('🔄 Resetting filters');

    // Reset form
    $('#filterForm')[0].reset();

    // Reset filter object
    currentFilter = {
        searchTerm: '',
    categoryId: null,
    status: null,
    dateRange: '',
    dateFrom: '',
    dateTo: '',
    sortBy: 'date_desc',
    page: 1,
    pageSize: 20
    };

    loadNewsList();
}

    function clearAllFilters() {
        resetFilters();
    showNotification('Đã xóa tất cả bộ lọc', 'info');
}

    function clearSearch() {
        $('#searchTerm').val('');
    $('#quickSearchResults').hide();
    currentFilter.searchTerm = '';
    currentFilter.page = 1;
    loadNewsList();
}

    // ===== QUICK SEARCH =====
    function performQuickSearch(term) {
        console.log('🔍 Quick search:', term);

    $.get('/News/QuickSearch', {term: term, maxResults: 10 }, function(data) {
        if (data.success && data.results.length > 0) {
        displayQuickSearchResults(data.results);
        } else {
        $('#quickSearchResults').hide();
        }
    }).fail(function() {
        $('#quickSearchResults').hide();
    });
}

    function displayQuickSearchResults(results) {
    const html = results.map(item => `
    <div class="quick-search-item" onclick="selectQuickSearchItem(${item.Id}, '${escapeHtml(item.Title)}')">
        <div class="title">${highlightSearchTerm(item.Title, currentFilter.searchTerm)}</div>
        <div class="meta">
            <span class="date">${formatDate(item.CreatedDate)}</span>
            <span class="status badge badge-${item.Status ? 'success' : 'secondary'}">${item.Status ? 'Hoạt động' : 'Tạm dừng'}</span>
        </div>
    </div>
    `).join('');

    $('#quickSearchResults').html(html).show();
}

    function selectQuickSearchItem(id, title) {
        window.location.href = `/News/Details/${id}`;
}

    // ===== NEWS LIST LOADING =====
    function loadNewsList() {
    if (isLoading) return;

    console.log('📰 Loading news list with filter:', currentFilter);

    isLoading = true;
    showLoading();

    $.post('/News/GetNewsListPartial', currentFilter, function(data) {
        if (data.success) {
        $('#newsListContainer').html(data.html);
    updatePaginationInfo(data);
    updateUrl();
    resetBulkSelection();
        } else {
        showNotification('Lỗi tải danh sách tin tức: ' + data.message, 'error');
        }
    }).fail(function(xhr, status, error) {
        console.error('❌ Error loading news list:', error);
    showNotification('Lỗi kết nối khi tải tin tức', 'error');
    }).always(function() {
        isLoading = false;
    hideLoading();
    });
}

    function handlePaginationClick(e) {
        e.preventDefault();

    const page = parseInt($(this).data('page'));
    if (page && page !== currentFilter.page) {
        currentFilter.page = page;
    loadNewsList();

    // Scroll to top
    $('html, body').animate({scrollTop: $('.news-list-card').offset().top - 100 }, 300);
    }
}

    // ===== BULK ACTIONS =====
    function showBulkActions() {
        $('#bulkActionsPanel').slideDown();
}

    function hideBulkActions() {
        $('#bulkActionsPanel').slideUp();
    resetBulkSelection();
}

    function handleNewsSelection() {
    const newsId = parseInt($(this).val());
    const isChecked = $(this).is(':checked');

    if (isChecked) {
        selectedNewsIds.add(newsId);
    } else {
        selectedNewsIds.delete(newsId);
    }

    updateBulkSelectionUI();
}

    function handleSelectAll() {
    const isChecked = $(this).is(':checked');

    $('.news-checkbox').prop('checked', isChecked);

    if (isChecked) {
        $('.news-checkbox').each(function () {
            selectedNewsIds.add(parseInt($(this).val()));
        });
    } else {
        selectedNewsIds.clear();
    }

    updateBulkSelectionUI();
}

    function updateBulkSelectionUI() {
    const count = selectedNewsIds.size;
    $('#selectedCount').text(count);

    if (count > 0 && $('#bulkActionsPanel').is(':hidden')) {
        showBulkActions();
    } else if (count === 0 && $('#bulkActionsPanel').is(':visible')) {
        hideBulkActions();
    }
}

    function resetBulkSelection() {
        selectedNewsIds.clear();
    $('.news-checkbox, #selectAllNews').prop('checked', false);
    $('#selectedCount').text('0');
}

    function bulkAction(action) {
    if (selectedNewsIds.size === 0) {
        showNotification('Vui lòng chọn ít nhất một tin tức', 'warning');
    return;
    }

    const actionTexts = {
        activate: 'kích hoạt',
    deactivate: 'tạm dừng',
    delete: 'xóa'
    };

    const actionText = actionTexts[action] || action;
    const confirmMessage = `Bạn có chắc muốn ${actionText} ${selectedNewsIds.size} tin tức đã chọn?`;

    if (!confirm(confirmMessage)) return;

    showLoading();

    $.post('/News/BulkAction', {
        action: action,
    selectedIds: Array.from(selectedNewsIds)
    }, function(data) {
        if (data.success) {
        showNotification(data.message, 'success');
    loadNewsList(); // Reload list
        } else {
        showNotification('Lỗi: ' + data.message, 'error');
        }
    }).fail(function() {
        showNotification('Lỗi kết nối khi thực hiện thao tác', 'error');
    }).always(function() {
        hideLoading();
    });
}

    // ===== QUICK ACTIONS =====
    function handleQuickAction(e) {
        e.preventDefault();

    const action = $(this).data('action');
    const newsId = $(this).data('news-id');
    const title = $(this).data('title');

    switch (action) {
        case 'toggle-status':
    toggleNewsStatus(newsId, title);
    break;
    case 'edit':
    window.location.href = `/News/Edit/${newsId}`;
    break;
    case 'delete':
    window.location.href = `/News/Delete/${newsId}`;
    break;
    case 'view':
    window.location.href = `/News/Details/${newsId}`;
    break;
    }
}

    function toggleNewsStatus(newsId, title) {
        $.post('/News/ToggleStatus', { id: newsId }, function (data) {
            if (data.success) {
                showNotification(data.message, 'success');

                // Update UI without full reload
                const statusBadge = $(`[data-news-id="${newsId}"]`).closest('.news-item').find('.status-badge');
                statusBadge.removeClass('badge-success badge-secondary')
                    .addClass(data.newStatus ? 'badge-success' : 'badge-secondary')
                    .text(data.newStatus ? 'Hoạt động' : 'Tạm dừng');
            } else {
                showNotification('Lỗi: ' + data.message, 'error');
            }
        }).fail(function () {
            showNotification('Lỗi kết nối', 'error');
        });
}

    // ===== UTILITY FUNCTIONS =====
    function toggleAdvancedFilter() {
        $('#advancedFilters').slideToggle();
}

    function toggleViewMode(mode) {
    const container = $('#newsListContainer');

    if (mode === 'grid') {
        container.addClass('grid-view').removeClass('list-view');
    } else {
        container.addClass('list-view').removeClass('grid-view');
    }
}

    function refreshNews() {
        loadNewsList();
    showNotification('Đã làm mới danh sách tin tức', 'info');
}

    function exportNews() {
        // Implementation for export functionality
        showNotification('Chức năng xuất Excel đang được phát triển', 'info');
}

    function updatePaginationInfo(data) {
        $('#totalNewsCount').text(data.totalCount);
}

    function updateUrl() {
    const params = new URLSearchParams();

    Object.keys(currentFilter).forEach(key => {
        const value = currentFilter[key];
    if (value !== null && value !== '' && value !== 0 && !(key === 'page' && value === 1)) {
        params.set(key, value);
        }
    });

    const newUrl = window.location.pathname + (params.toString() ? '?' + params.toString() : '');
    history.replaceState(null, null, newUrl);
}

    function showLoading() {
        $('#loadingOverlay').fadeIn(200);
}

    function hideLoading() {
        $('#loadingOverlay').fadeOut(200);
}

    function showNotification(message, type = 'info') {
    const alertClass = `alert-${type === 'error' ? 'danger' : type}`;
    const iconClass = type === 'success' ? 'fa-check-circle' :
    type === 'error' || type === 'danger' ? 'fa-exclamation-triangle' :
    type === 'warning' ? 'fa-exclamation-circle' : 'fa-info-circle';

    const notification = $(`
    <div class="alert ${alertClass} alert-dismissible fade show notification-toast"
        style="position: fixed; top: 20px; right: 20px; z-index: 9999; min-width: 300px;">
        <i class="fas ${iconClass} mr-2"></i>
        ${message}
        <button type="button" class="close" data-dismiss="alert">
            <span>&times;</span>
        </button>
    </div>
    `);

    $('body').append(notification);

    setTimeout(function() {
        notification.fadeOut(function () { $(this).remove(); });
    }, 5000);
}

    // ===== KEYBOARD SHORTCUTS =====
    function handleKeyboardShortcuts(e) {
    // Ctrl+N: New news
    if (e.ctrlKey && e.keyCode === 78) {
        e.preventDefault();
    window.location.href = '/News/Create';
    }

    // Ctrl+R: Refresh
    if (e.ctrlKey && e.keyCode === 82) {
        e.preventDefault();
    refreshNews();
    }

    // Escape: Clear search/hide panels
    if (e.keyCode === 27) {
        $('#quickSearchResults').hide();
    if ($('#bulkActionsPanel').is(':visible')) {
        hideBulkActions();
        }
    }
}

    // ===== HELPER FUNCTIONS =====
    function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

    function highlightSearchTerm(text, term) {
    if (!term) return text;
    const regex = new RegExp(`(${term})`, 'gi');
    return text.replace(regex, '<mark>$1</mark>');
}

    function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('vi-VN');
}

    // ===== AUTO-DISMISS ALERTS =====
    setTimeout(function() {
        $('.notification-alert').fadeOut();
}, 5000);

    console.log('✅ News Management initialized successfully');
