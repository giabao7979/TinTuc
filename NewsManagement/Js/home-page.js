// ===== DEBUG SCRIPT - Thêm vào console để test =====

// Test API trực tiếp
function testAPI() {
    console.log('Testing GetNewsByCategory API...');

    $.ajax({
        url: '/Home/GetNewsByCategory',
        type: 'GET',
        data: { categoryId: 1, page: 1, pageSize: 20 },
        dataType: 'json',
        success: function (data) {
            console.log('✅ API Success:', data);

            if (data.success && data.data && data.data.length > 0) {
                console.log('✅ Found news:', data.data.length, 'items');
                console.log('First news item:', data.data[0]);

                // Test display function
                displayCategoryNews(data.data, 'Test Category', data.totalCount);
                console.log('✅ Display function called');
            } else {
                console.log('❌ No news data in response');
            }
        },
        error: function (xhr, status, error) {
            console.log('❌ API Error:', {
                status: status,
                error: error,
                responseText: xhr.responseText,
                statusCode: xhr.status
            });
        }
    });
}

// Test category click event
function testCategoryClick() {
    console.log('Testing category click...');

    // Simulate clicking on first category
    var firstCategory = $('.category-link').first();
    if (firstCategory.length > 0) {
        var categoryId = firstCategory.data('category-id');
        var categoryName = firstCategory.data('category-name');

        console.log('Found first category:', categoryId, categoryName);

        if (categoryId && categoryName) {
            loadNewsByCategory(categoryId, categoryName);
        } else {
            console.log('❌ Category data attributes missing');
        }
    } else {
        console.log('❌ No categories found');
    }
}

// Check if all required elements exist
function checkElements() {
    console.log('Checking required elements...');

    var checks = {
        'Categories menu': $('#categories-menu').length > 0,
        'Content container': $('#content-container').length > 0,
        'Search input': $('#search-input').length > 0,
        'Content title': $('#content-title').length > 0,
        'jQuery loaded': typeof $ !== 'undefined'
    };

    for (var check in checks) {
        console.log(check + ':', checks[check] ? '✅' : '❌');
    }

    // Check categories
    var categoryCount = $('.category-link').length;
    console.log('Category links found:', categoryCount);

    if (categoryCount > 0) {
        console.log('First category data:', {
            id: $('.category-link').first().data('category-id'),
            name: $('.category-link').first().data('category-name')
        });
    }
}

// Run all tests
function runAllTests() {
    console.log('🔍 Starting debug tests...');
    console.log('========================');

    setTimeout(function () {
        checkElements();
        console.log('========================');
        testAPI();
        console.log('========================');
        testCategoryClick();
    }, 1000);
}

// Auto-run when ready
$(document).ready(function () {
    // Add test button to page
    if ($('#debug-panel').length === 0) {
        $('body').append(`
            <div id="debug-panel" style="position: fixed; top: 10px; right: 10px; z-index: 9999; background: #fff; border: 1px solid #ccc; padding: 10px; border-radius: 5px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);">
                <h6>Debug Panel</h6>
                <button onclick="runAllTests()" class="btn btn-sm btn-info">Run Tests</button>
                <button onclick="testAPI()" class="btn btn-sm btn-warning">Test API</button>
                <button onclick="testCategoryClick()" class="btn btn-sm btn-success">Test Click</button>
                <button onclick="checkElements()" class="btn btn-sm btn-secondary">Check Elements</button>
            </div>
        `);
    }

    console.log('🐛 Debug panel added. Use runAllTests() to start debugging.');
});

// Additional helper functions
function logAllEventListeners() {
    console.log('Category links with events:', $('.category-link').length);
    $('.category-link').each(function (index, element) {
        var $el = $(element);
        console.log('Category', index + 1, ':', {
            id: $el.data('category-id'),
            name: $el.data('category-name'),
            hasClickEvent: $._data(element, "events") && $._data(element, "events").click
        });
    });
}

function simulateAPIResponse() {
    console.log('Simulating API response...');

    var mockData = {
        success: true,
        data: [
            {
                Id: 1,
                Title: "Tin tức test 1",
                Summary: "Đây là tin tức test số 1 với nội dung mô tả ngắn gọn...",
                CreatedDate: "06/06/2024",
                Categories: ["Công nghệ", "AI"]
            },
            {
                Id: 2,
                Title: "Tin tức test 2",
                Summary: "Đây là tin tức test số 2 với nội dung khác...",
                CreatedDate: "05/06/2024",
                Categories: ["Kinh doanh"]
            }
        ],
        totalCount: 2,
        categoryName: "Test Category"
    };

    displayCategoryNews(mockData.data, mockData.categoryName, mockData.totalCount);
    console.log('✅ Mock data displayed');
}