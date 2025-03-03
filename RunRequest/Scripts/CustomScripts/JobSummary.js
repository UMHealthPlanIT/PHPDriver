// Polyfill to allow every() function in IE
// https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Array/every#Polyfill
if (!Array.prototype.every) {
    Array.prototype.every = function (callbackfn, thisArg) {
        'use strict';
        var T, k;

        if (this == null) {
            throw new TypeError('this is null or not defined');
        }

        // 1. Let O be the result of calling ToObject passing the this 
        //    value as the argument.
        var O = Object(this);

        // 2. Let lenValue be the result of calling the Get internal method
        //    of O with the argument "length".
        // 3. Let len be ToUint32(lenValue).
        var len = O.length >>> 0;

        // 4. If IsCallable(callbackfn) is false, throw a TypeError exception.
        if (typeof callbackfn !== 'function') {
            throw new TypeError();
        }

        // 5. If thisArg was supplied, let T be thisArg; else let T be undefined.
        if (arguments.length > 1) {
            T = thisArg;
        }

        // 6. Let k be 0.
        k = 0;

        // 7. Repeat, while k < len
        while (k < len) {

            var kValue;

            // a. Let Pk be ToString(k).
            //   This is implicit for LHS operands of the in operator
            // b. Let kPresent be the result of calling the HasProperty internal 
            //    method of O with argument Pk.
            //   This step can be combined with c
            // c. If kPresent is true, then
            if (k in O) {

                // i. Let kValue be the result of calling the Get internal method
                //    of O with argument Pk.
                kValue = O[k];

                // ii. Let testResult be the result of calling the Call internal method
                //     of callbackfn with T as the this value and argument list 
                //     containing kValue, k, and O.
                var testResult = callbackfn.call(T, kValue, k, O);

                // iii. If ToBoolean(testResult) is false, return false.
                if (!testResult) {
                    return false;
                }
            }
            k++;
        }
        return true;
    };
}

function FilterSelections(field) {
    var filterType = $(field.currentTarget).attr('class').split(' ')[0];
    var filterValue = $(field.currentTarget).attr('value');

    if (filterValue == "All") {
        $(".card").show();
        $(".job-item").show();
    } else {
        if (filterType == "DepartmentFilter") {

            SearchFilter(filterValue, "department")

        } else if (filterType == "TypeFilter") {

            if (filterValue == "JobsICanRun") {

                SearchFilter(filterValue, "runaccess");

            } else {

                SearchFilter(filterValue, "jobtype");

            }

        } else if (filterType == "FormatFilter") {

            SearchFilter(filterValue, "format");

        }
    }


    //$("input:checkbox." + filterType).not(field.currentTarget).prop('checked', false);
    //$("input:checkbox").not(field.currentTarget).prop('checked', false);

}

function SearchFilter(searchCriteria, filterType) {

    var cards = $(".card");
    var rows = $(".job-item");

    if (filterType != "Search") { //start with everything shown
        cards.show();
        rows.show();
    }

    if (searchCriteria == "") {
        cards.show();
        rows.show();
    } else {
        cards.each(function () { onlyShowMatchingCards($(this), searchCriteria, filterType) });
        rows.each(function () { onlyShowMatchingCards($(this), searchCriteria, filterType) });
    }

}

function onlyShowMatchingCards(card, searchCriteria, filterType) {

    if (filterType == "Search") {
        // Filters on letters and numbers independently. Ignores everything else.
        var filteredSearch = searchCriteria.match(/[a-zA-Z]+|[\d]+/g);

        // Can't use '=>' in IE, below is the equivalent of:
        // if (filteredSearch.every(item => card.text().toLowerCase().indexOf(item) > -1))
        if (filteredSearch.every(function (item) {
            return card.text().toLowerCase().indexOf(item) > -1;
        })) {
            $(card).show();
        } else {
            $(card).hide();
        }

    }
    else {
            if ($(card).data(filterType).indexOf(searchCriteria) != -1) {
                $(card).show();
            } else {
                $(card).hide();
            }
    }


}

var OnClickSave = false;

function SaveFavorite(obj) {

    var savedJob = $(obj);

    savedJob.toggleClass("fa-star-o")
    savedJob.toggleClass("fa-star");

}


function ShowTab(clickedTab) {
    $(".tab-content").not("#" + clickedTab).hide();
    $("#" + clickedTab).show();

}

function SwitchView(field) {
    //var filterType = $(field.currentTarget).attr('class');
    var filterValue = $(field.currentTarget).attr('value');

    if (filterValue == "List") {
        $('#cardDeckContent').hide()
        $('#listView').show()
    } else {
        $('#cardDeckContent').show()
        $('#listView').hide()
    }

    if ($('#searchfield').val() != "") {
        SearchFilter($('#searchfield').val().toLowerCase(), 'Search')
    }
}



$(document).ready(function () {
    $("input:checkbox").change(FilterSelections)
    //$("input:radio").change(FilterSelections)
    $('.selections').change(FilterSelections)
    $('.ViewFilter').change(SwitchView)
    

    if ($("#runnableJobsFilter") != undefined) {
        $("#runnableJobsFilter").prop("checked", true)
        $("#runnableJobsFilter").trigger('change')
    }

}
);