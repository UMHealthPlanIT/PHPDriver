var controllerActions = {};

function Init(baseUrl) {
    controllerActions = {
        CheckSource: baseUrl + "/CheckSource",
        GetAllJobsForDate: (date) => baseUrl + `/GetAllJobs?date=${date}`,
        GetErroredJobs: baseUrl + "/GetErroredJobs",
        GetLogs: baseUrl + "/GetLogs",
        GetScheduleForDate: (date) => baseUrl + `/GetSchedule?date=${date}`
    };
}

function LoadScheduleView() {
    var $view = $("#todaysSchedule");
    var $viewdate = $("#datepicker").datepicker("option", "dateFormat", "yy-mm-dd").val();
    var $url = controllerActions.GetScheduleForDate($viewdate);

    $.ajax({
        url: $url,
        cache: false,
        success: function (data) {
            $view.html(data);
            SetFilters();
        }
    });
    $('#scheduleTimeStamp').html(GetDateTime());
}

function LoadErroredJobsView() {
    $('#erroredJobs').load(controllerActions.GetErroredJobs);
    SetFilters();
}

function LoadAllJobsView() {
    var $view = $("#allJobs");
    var $viewdate = $("#datepicker").datepicker("option", "dateFormat", "yy-mm-dd").val();
    var $url = controllerActions.GetAllJobsForDate($viewdate);

    $.ajax({
        url: $url,
        cache: false,
        success: function (data) {
            $view.html(data);
            SetFilters();
        }
    });
    $('#scheduleTimeStamp').html(GetDateTime());
}

function GetDateTime() {
    var today = new Date();
    return today.toLocaleTimeString();
}

function UpdateErrorCount(sparrow, php) {
    var errors = document.getElementById("errorList").getElementsByTagName("a")
    var count = 0;

    for (let i = 0; i < errors.length; i++) {
        if ((errors[i].classList.contains('SPARROW') && sparrow)
            || (errors[i].classList.contains('PHP') && php)
            || (!errors[i].classList.contains('PHP') && !errors[i].classList.contains('SPARROW'))) {
            count += 1;
        }
    }

    document.getElementById('errorCount').innerHTML = count;
    ChangeTitle(count);
}

function ChangeTitle(count) {
    var newTitle = '(' + count + ') Job Console';
    document.title = newTitle;
}

function SearchJobs(searchCriteria) {
    $(".jobpill").each(function (index, element) {
        if (searchCriteria == "Search" || searchCriteria == "") { //start with everything shown
            $(this).parent().show();
        }
        else {
            if ($(this).text().toLowerCase().indexOf(searchCriteria) > -1) {
                $(this).parent().show();
            }
            else {
                $(this).parent().hide();
            }
        }
    });
}

function ApplyFilter(filter, display, sparrow, php) {
    $(".jobpill").each(function (index, element) {
        if (filter == 'jobs' || filter == 'sparrow' || filter == 'php') {
            if (display) {
                if ($(this).text().toLowerCase().indexOf('controller') == -1 && $(this).text().toLowerCase().indexOf('worker') == -1 && $(this).text().toLowerCase().indexOf('web') == -1 && $(this).text().toLowerCase().indexOf('ulogger') == -1) {
                    $(this).parent().hide();
                    if ((sparrow && $(this).hasClass('SPARROW'))
                        || (php && $(this).hasClass('PHP'))
                        || (!$(this).hasClass('SPARROW') && !$(this).hasClass('PHP'))) {
                        $(this).parent().show();
                        }
                    }
                }
            else {
                if ($(this).text().toLowerCase().indexOf('controller') == -1 && $(this).text().toLowerCase().indexOf('worker') == -1 && $(this).text().toLowerCase().indexOf('web') == -1 && $(this).text().toLowerCase().indexOf('ulogger') == -1) {
                    if (filter == 'jobs' 
                        || ((!sparrow && $(this).hasClass('SPARROW'))
                        || (!php && $(this).hasClass('PHP')))) {
                        $(this).parent().hide();
                    }
                }
            }
        }
        else if (filter == 'system') {
            if (display) {
                if ($(this).text().toLowerCase().indexOf('controller') > -1 || $(this).text().toLowerCase().indexOf('worker') > -1 || $(this).text().toLowerCase().indexOf('ulogger') > -1) {
                    $(this).parent().show();
                }
            }
            else {
                if ($(this).text().toLowerCase().indexOf('controller') > -1 || $(this).text().toLowerCase().indexOf('worker') > -1 || $(this).text().toLowerCase().indexOf('ulogger') > -1) {
                    $(this).parent().hide();
                }
            }
        }
        else { //Web jobs essentially
            if ($(this).text().toLowerCase().indexOf(filter) > -1) {
                if (display) {
                    $(this).parent().show();
                }
                else {
                    $(this).parent().hide();
                }
            }
        }
    });
}

function NextError(currentErrorNumber, totalNumberOfExecutions) {
    var newNext;
    var newLast;
    var totalNumberOfErrors = $('tr[id^="ERROR"]').length;
    if (currentErrorNumber <= totalNumberOfErrors) {
        if (currentErrorNumber > totalNumberOfErrors + 1) {
            newNext = totalNumberOfErrors + 1;
        }
        else {
            newNext = currentErrorNumber + 1;
        }
        newLast = newNext - 2;

        var currentErrorJump = "#ERROR" + currentErrorNumber;
        $('#nextError').attr('onClick', 'NextError(' + newNext + ',' + totalNumberOfExecutions + ')');
        $('#lastError').attr('onClick', 'LastError(' + newLast + ',' + totalNumberOfExecutions + ')');

        var parentIdLast = $('#ERROR' + newLast).parent().parent().parent().parent().attr('id');
        var parentIdCurrent = $('#ERROR' + currentErrorNumber).parent().parent().parent().parent().attr('id');

        if (parentIdLast != parentIdCurrent && totalNumberOfExecutions > 1) {
            $("[aria-controls='" + parentIdLast + "']").click();
            $("[aria-controls='" + parentIdCurrent + "']").click();
        }

        document.getElementById("ERROR" + currentErrorNumber).scrollIntoView();
    }
}

function LastError(currentErrorNumber, totalNumberOfExecutions) {
    var newLast;
    var newNext;

    newNext = currentErrorNumber + 1;
    newLast = newNext - 2;

    if (currentErrorNumber > 0) {
        if (newNext < 1) {
            newNext = 1;
        }
        if (newLast < 0) {
            newLast = 0;
        }

        $('#nextError').attr('onClick', 'NextError(' + newNext + ',' + totalNumberOfExecutions + ')');
        $('#lastError').attr('onClick', 'LastError(' + newLast + ',' + totalNumberOfExecutions + ')');

        var parentIdNext = $('#ERROR' + newNext).parent().parent().parent().parent().attr('id');
        var parentIdCurrent = $('#ERROR' + currentErrorNumber).parent().parent().parent().parent().attr('id');
        if (parentIdNext != parentIdCurrent & totalNumberOfExecutions > 1) {
            $("[aria-controls='" + parentIdNext + "']").click();
            $("[aria-controls='" + parentIdCurrent + "']").click();
        }

        document.getElementById('ERROR' + currentErrorNumber).scrollIntoView();
    }

}

function sleep(milliseconds) {
    const date = Date.now();
    let currentDate = null;
    do {
        currentDate = Date.now();
    } while (currentDate - date < milliseconds);
}

function GetLogs(jobIndex, dateTicks, owner) {
    var loader = '<div class="loadinglogs"><div class="row"><div class="col-md-2 col-md-offset-6" style="width: 85px; top: 250px;"><span class="fa fa-spinner fa-spin fa-5x"></span></div></div></div >';
    $('#logView').html(loader);
    console.log('owner is ' + owner);
    $.ajax({
        url: controllerActions.GetLogs,
        type: 'GET',
        cache: false,
        data: { jobIndex: jobIndex, dateTicks: dateTicks, owner: owner },
        success: function (response) {
            $('#logView').html(response);
        },
        error: function (response) {
            $('#logView').removeClass('dim');
            alert(response.responseText);
        }
    });
}

function SetCookie(cname, cvalue, exdays) {
    const d = new Date();
    d.setTime(d.getTime() + (exdays * 24 * 60 * 60 * 1000));
    let expires = "expires=" + d.toUTCString();
    document.cookie = cname + "=" + cvalue + ";" + expires + ";path=/";
}

function GetCookie(cname) {
    let name = cname + "=";
    let ca = document.cookie.split(';');
    for (let i = 0; i < ca.length; i++) {
        let c = ca[i];
        while (c.charAt(0) == ' ') {
            c = c.substring(1);
        }
        if (c.indexOf(name) == 0) {
            return c.substring(name.length, c.length);
        }
    }
    return "";
}

function CallDataSources(datasources) {
    for (var source of datasources) {
        $.ajax({
            url: controllerActions.CheckSource,
            type: 'GET',
            timeout: 30000,
            async: true,
            data: { dataSource: source }
        }).success(function (response) {
            if (response.manualSwitch && response.sourceReady) {
                $('input[id="manual-' + response.name.replace(/\s/g, '|') + '"]').prop("checked", true);
            }
            else {
                $('input[id="manual-' + response.name.replace(/\s/g, '|') + '"]').prop("checked", false);
            }
            if (response.sourceReady) {
                setReady(response);
            } else {
                setNotReady(response);
            }
            if (response.overrideReadinessQuery) {
                $('input[id="override-' + response.name.replace(/\s/g, '|') + '"]').prop("checked", true);
            }
            else {
                $('input[id="override-' + response.name.replace(/\s/g, '|') + '"]').prop("checked", false);
            }
        }).error(function (XMLHttpRequest, textStatus, errorThrown) {
            console.log("timeout");
            if (textStatus == "timeout") {
                setTimeout(source);
            }
            //console.log(response.responseText);
        }
        );
    }
}


function sourceReadyCheck(datasource) {
    if (datasource.sourceReady) {
        setReady(datasource)
    }
    else {
        setNotReady(datasource)
    }
}

function setReady(datasource) {
    $('#' + datasource.name.replace(/[\s\(\)]/g, '')).addClass('glyphicon-ok-sign');
    $('#' + datasource.name.replace(/[\s\(\)]/g, '')).removeClass('glyphicon-remove-sign');
    $('#' + datasource.name.replace(/[\s\(\)]/g, '')).removeClass('glyphicon-exclamation-sign');
    $('#' + datasource.name.replace(/[\s\(\)]/g, '')).css("color", "lawngreen");
}

function setNotReady(datasource) {
    $('#' + datasource.name.replace(/[\s\(\)]/g, '')).removeClass('glyphicon-ok-sign')
    $('#' + datasource.name.replace(/[\s\(\)]/g, '')).removeClass('glyphicon-exclamation-sign')
    $('#' + datasource.name.replace(/[\s\(\)]/g, '')).addClass('glyphicon-remove-sign')
    $('#' + datasource.name.replace(/[\s\(\)]/g, '')).css("color", "red");
}

function setTimeout(datasource) {
    $('#' + datasource.name.replace(/[\s\(\)]/g, '')).removeClass('glyphicon-ok-sign')
    $('#' + datasource.name.replace(/[\s\(\)]/g, '')).removeClass('glyphicon-remove-sign')
    $('#' + datasource.name.replace(/[\s\(\)]/g, '')).addClass('glyphicon-exclamation-sign')
    $('#' + datasource.name.replace(/[\s\(\)]/g, '')).css("color", "orange");
}