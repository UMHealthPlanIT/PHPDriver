$(document).ready(function () {

    // These lines make dates in the specified format sort properly 🧙‍♂️ (That's a wizard)
    $.fn.dataTable.moment('M/D/YYYY h:mm:ss A');
    $.fn.dataTable.moment('MM/DD/YYYY');

    // Initialize DataTable
    var table = $('#table1').DataTable({
        "dom": '<"top"l>rt<"bottom"ip>',
        "lengthMenu": [[100, 50, 25, 10, -1], [100, 50, 25, 10, "All"]],
        "order": [[1, "asc"]],
        "columnDefs": [{ "orderable": false, "targets": 0 }], //this specifies that the first column is not sortable (because it is the edit/delete buttons)
        fixedHeader: {
            header: true,
            headerOffset: 50
        }
    });

    // Add Select elements to perform filter
    $('.filter-select').each(function (i) {
        var select = $('<select class="form-control"><option value=""></option></select>')
            .appendTo($(this).empty()).on('change', function () {
                var val = $(this).val();
                clearOtherSelects(this);
                table.column(i + 1).search(val, false, false).draw();
            });

        table.column(i + 1).data().unique().sort().each(function (d, j) {
            select.append('<option value="' + d + '">' + d + '</option>')
        });
    });

    // Clear "other" filters when a new filter is selected (single column filtering)
    function clearOtherSelects(currentSelect) {
        $('.filter-select > select').each(function (i) {
            if (currentSelect != this) {
                this.selectedIndex = 0;

                table.column(i + 1).search("", false, false).draw();
            }
        });
    }

    // Handle bulk updates if table allows
    if ($('#bulkDiv').length > 0) {
        $('#table1_length').after($('#bulkDiv').removeClass('hidden'));
    }

    // Add toggle functionality to the filter row (saves space and prevents page jump from rendering)
    $('#table1_length').after('<button id="toggleTableFilter" type="button" class="btn btn-primary" style="font-weight: bold; margin-left: 20px">Toggle Filters</button>');
    $("#toggleTableFilter").click(function () {
        $("#customTableFilter").toggle();
    });

    // Adds our custom search element, with default DataTable search behavior
    $('#table1_length').after('<input id="customSearch" type="search" class="jobSearchBox2" placeholder="Search Table" style= "border-radius: 4px"/>');
    $("#customSearch").on('keyup', function () {
        table.search(this.value).draw();
    });

});