var baseAppUrl;

var controllerActions = () => ({
    DeleteSchedule: baseAppUrl + "Home/DeleteSchedule",
    DownloadAttachments: (jobId) => baseAppUrl + "Home/DownloadAttachments?jobId=" + jobId,
    DownloadFile: (fileGuid, fileName) => baseAppUrl + `Home/Download?fileGuid=${fileGuid}&fileName=${fileName}`,
    EditSchedule: baseAppUrl + "Home/EditSchedule",
    GetJobConfig: baseAppUrl + "Home/GetJobConfig",
    HelpPage: baseAppUrl + "Home/HelpPage",
    JobHistory: baseAppUrl + "Home/HistoryTable",
    LaunchDetails: baseAppUrl + "Home/LaunchDetails",
    LaunchJobToController: baseAppUrl + "Home/LaunchJobToController",
    LaunchParameterJob: baseAppUrl + "Home/LaunchParameterJob",
    RequestRun: (productId) => baseAppUrl + 'api/RequestRun/' + productId,
    RunAdHocJob: baseAppUrl + "JobCentralControl/RunJobAdhoc",
    RunStatus: baseAppUrl + "Home/GetJobsCurrentRunStatus",
    ScheduleDetails: baseAppUrl + "Home/GetScheduleDetails",
    ScheduleForm: baseAppUrl + "Home/ScheduleForm",
    WebRequestListData: baseAppUrl + "IWebRequestData/GetListData"
});

function refreshStatus() {
    $('.jobStatus:visible').each(function (index) {
        $(this).show();
        var jobId = this.getAttribute("id");

        var jobType = $(this).data("jobtype");

        if (isInViewport(this)) {
            getStatus(jobId, "#" + jobId + "img", "#" + jobId + "val", true, false, jobType, viewType="card");
        }
    });
    $('.jobStatus-list:visible').each(function (index) {
        $(this).show();
        var jobId = this.getAttribute("id").split("-")[0];

        var jobType = $(this).data("jobtype");

        if (isInViewport(this)) {
            getStatus(jobId, "#" + jobId + "img-list", "#" + jobId + "-list-val", true, false, jobType, viewType = "list");
        }
    });
}

function getStatus(jobIndex, JobStatusImgPointer, JobStatusTextPointer, refreshTimer, bigImage, jobType, viewType) {
    
    $.ajax({
        url: controllerActions().RunStatus,
        cache: false,
        data: { jobId: jobIndex, fromIndex: false },
        success: function (data) {
            if (data == "None") {
                if (viewType == "card") {
                    $(JobStatusTextPointer).html("");
                    addStatusImg(JobStatusImgPointer, data, bigImage);
                }     
            } else {
                var text = data;
                if (data == "OnHold") {
                    text = "On Hold";
                }
                else if (data == "DataSourceNotReady") {
                    text = "Data Source Not Ready";
                }
                if (jobType.indexOf("WebReport") == -1 || data == "DataSourceNotReady") { //Don't show the status for run request jobs unless its telling us we can't run it
                    if (viewType == "card") {
                        $(JobStatusTextPointer).html(text);
                        addStatusImg(JobStatusImgPointer, data, bigImage);
                    } else {
                        $(JobStatusTextPointer).children()[1].textContent=text;
                        addStatusImg(JobStatusImgPointer, data, bigImage);
                    }
                    
                } else if (jobType.indexOf("WebReport") > -1) {

                    if (viewType == "card") {
                        $(JobStatusTextPointer).html("");
                        addStatusImg(JobStatusImgPointer, "", bigImage);
                    } else {
                        $(JobStatusTextPointer).text("");
                        addStatusImg(JobStatusImgPointer, data, bigImage);
                    }
        
                }

            }

            if (data != "None") {
                $("#" + jobIndex).show();
            }

            if (!refreshTimer) {
                canRunJob(data);
            }

        }
    });
}

function canRunJob(responseData) {
    if (responseData == "None" || responseData == "Finished") {
        $("#launchButton").removeAttr('disabled');
    } else {
        $("#launchButton").attr('disabled', 'disabled');
    }
}

function addStatusImg(statusSelector, state, makeBigger) {
    var obj = $(statusSelector);

    if (state == "Queued") {
        if (!obj.hasClass("QueuedStatus")) {
            obj.removeAttr("class");
            if (makeBigger) {
                obj.addClass("fa fa-calendar fa-2x");
            } else {
                obj.addClass("fa fa-calendar");
            }

            obj.addClass("QueuedStatus");
        }
    } else if (state == "Started") {
        if (!obj.hasClass("StartedStatus")) {
            obj.removeAttr("class");
            if (makeBigger) {
                obj.addClass("fa fa-truck fa-2x");
            } else {
                obj.addClass("fa fa-truck");
            }

            obj.addClass("StartedStatus");
        }
    } else if (state == "Finished") {
        if (!obj.hasClass("FinishedStatus")) {
            obj.removeAttr("class");

            if (makeBigger) {
                obj.addClass("fa fa-flag-checkered fa-2x");
            } else {
                obj.addClass("fa fa-flag-checkered");
            }

            obj.addClass("FinishedStatus");
        }
    } else if (state == "Errored") {
        if (!obj.hasClass("ErroredStatus")) {
            obj.removeAttr("class");

            if (makeBigger) {
                obj.addClass("fa fa-bug fa-2x");
            } else {
                obj.addClass("fa fa-bug");
            }

            obj.addClass("ErroredStatus");
        }
    } else if (state == "OnHold" || state == "DataSourceNotReady") {
        if (!obj.hasClass("HoldStatus")) {
            obj.removeAttr("class");
            if (makeBigger) {
                obj.addClass("fa fa-pause-circle fa-2x");
            } else {
                obj.addClass("fa fa-pause-circle");
            }
            
            obj.addClass("HoldStatus");
        }
    }
}

function setupStatus(appUrl) {
    baseAppUrl = appUrl;
    $('.jobStatus').each(function (index) { //show all jobs that are in a status other than 'nothing' (queued, started, finished, etc.)
        var jobIndex = this.getAttribute("id");
        var jobStatusImg = "#" + jobIndex + "img";
        var initialRunState = this.getAttribute("class").split(' ')[1];
        var jobType = $(this).data("jobtype");

        if (jobType != "WebReport" || initialRunState == "DataSourceNotReady") {
            addStatusImg(jobStatusImg, initialRunState, false);
        }

        $(this).show();
    });

    setInterval(refreshStatus, 60000); //30 seconds
}

function JobDetails(productId, owner, refresh = false) {
    if (!refresh) {
        $('.loading').modal('show');
    } 
    $('.scheduleSpinner').show();
    $('.cards').each(function () {
        $(this).attr('id', $(this).attr('onclick'));
        $(this).removeAttr("onclick");
    });
    $("a").each(function () {
        $(this).css('pointer-events', 'none');
    });    
    $.ajax({
        url: controllerActions().LaunchDetails,
        data: { jobId: productId },
        success: function (data) {
            $('#modalWrapper').html(data);
            $('#editModal').modal('show');
            $("a").each(function () {
                $(this).css('pointer-events', '');
            });
            $('.cards').each(function () {
                $(this).attr('onclick', $(this).attr('id'));
            });
        },
        error: function (data) {

        }
    });
    $('.loading').modal('hide');
}

function JobHistory(productId, page) {
    $("a").each(function () {
        $(this).css('pointer-events', 'none');
    });

    $.ajax({
        url: controllerActions().JobHistory,
        data: { jobId: productId, page: page },
        success: function (data2) {
            $('#historyTable').html(data2);
            $("a").each(function () {
                $(this).css('pointer-events', '');
            });
        }
    });
}

function JobSchedule(productId, page) {
    $(".scheduleError").remove();
    $('.scheduleSpinner').fadeIn();
    

    $.ajax({
        url: controllerActions().ScheduleDetails,
        data: { jobId: productId, page: page},
        success: function (data2) {
            $('.scheduleSpinner').fadeOut();
            $('#scheduleDetails').fadeIn();
            $('#scheduleDetails').html(data2);
        }
    });


}

function ScheduleForm(productId) {
    $('.displaySchedule').hide();
    $('.scheduleSpinner').show();
    $('#createSchedule').hide();
    $.ajax({
        url: controllerActions().ScheduleForm,
        data: { jobId: productId},
        success: function (data2) {
            $('.scheduleSpinner').hide();
            $('#scheduleArea').html(data2);
        }
    });

}

function DeleteSchedule(jobId, SchedulId) {
    $.ajax({
        url: controllerActions().DeleteSchedule,
        data: { jobId: jobId, scheduleId: SchedulId },
        success: function () {
            JobSchedule(jobId, 1);
        }
    })
}

function EditSchedule(jobId, SchduleId) {
    $.ajax({
        url: controllerActions().EditSchedule,
        data: { jobId: jobId, scheduleID: SchduleId },
        success: function (data2) {
            $('.scheduleSpinner').hide();
            $('#scheduleArea').html(data2);
            $('#scheduleDetails').fadeIn();
        }
    })
}


function JobConfig(productId) {

    $.ajax({
        url: controllerActions().GetJobConfig,
        data: { jobId: productId},
        success: function (data2) {
            $('#jobConfigLoader').hide();
            $('#jobConfiguration').html(data2);
            $('#jobConfiguration').fadeIn();
        }
    });

};


function formToJSON(elements) {
    return [].reduce.call(elements, function (data, element) {
        var className = " " + element.className + " ";

        if (className.replace(/[\n\t]/g, "").indexOf(" select2-search__field ") > -1)
            return data;

        if (element.attributes['data-optional'].value.toUpperCase() == "TRUE" && element.value == "")
            return data;

        if (data == null)
            return null;

        if (isSelect(element)) {
            data[element.name] = getSelectValues(element);
        }
        //use this for defaulting date to today
        //else if (element.classList.contains("hasDatepicker") && element.value == "") {
        //    data[element.name] = new Date().toJSON().slice(0, 10);
        //}
        else {
            if (element.value == "")
                return null;
            data[element.name] = element.value;
        }
        return data;

    }, {})
}


function getSelectValues(options) {
    return [].reduce.call(options, function (values, option) {
        return option.selected ? values.concat(option.value) : values;
    }, [])
}

function isSelect(element) {
    return element.options;
}

function LaunchJob(productId, tool, source, owner) {

    var runMode = $("input[name='runMode']:checked").val();

    //Save submitted file in all cases if there is one
    var fileInput = document.getElementById('file');
    if (fileInput != null && fileInput.files.length > 0) {
        var formData = new FormData();
        for (i = 0; i < fileInput.files.length; i++) {
            formData.append(fileInput.files[i].name, fileInput.files[i]);
        }
        formData.append("runMode", runMode);
        var xhr = new XMLHttpRequest();
        xhr.open('POST', controllerActions().RequestRun(productId), false);
        xhr.send(formData);
    }

    if (document.getElementById("parameterForm")) {
        console.log("parameterFrom");
        var multiListObjs = document.getElementsByClassName("select2-multi");
        var singleListObjs = document.getElementsByClassName("select2-single");
        if (multiListObjs.length > 0) {
            for (i = 0; i < multiListObjs.length; ++i) {
                if (multiListObjs[i].getAttribute("data-optional").toUpperCase() != "TRUE") {
                    if (multiListObjs[i].selectedIndex == -1) {
                        $(".fileWarning").show();
                        return;
                    }
                }
            }
        }
        if (singleListObjs.length > 0) {
            for (i = 0; i < singleListObjs.length; ++i) {
                if (singleListObjs[i].getAttribute("data-optional").toUpperCase() != "TRUE") {
                    if (singleListObjs[i].selectedIndex == 0) {
                        $(".fileWarning").show();
                        return;
                    }
                }
            }
        }

        var parameterObject = formToJSON(document.getElementById("parameterForm").elements);
        if (parameterObject == null) {
            $(".fileWarning").show();
            return;
        }
        var parameters = JSON.stringify(parameterObject, null, " ");

        $("#loadingDiv").removeClass("hidden");
        $('.modal-body, .modal-footer').each(function (i, div) {
            div.className += ' hidden'
        });
        console.log("LaunchParameterJob");
        $.ajax({
            url: controllerActions().LaunchParameterJob,
            data: { jobId: productId, tool: tool, parametersJson: parameters, owner: owner },
            cache: false,
            success: function (data) {
                if (data != "") {
                    if (data.IsFile) {
                        window.location = controllerActions().DownloadFile(data.FileGuid, data.FileName);
                        $("#editModal").modal('hide');
                    }
                    else {
                        $("#editModal").modal('hide');
                        JobDetails(productId);
                        alert(data.ErrorMessage);
                    }
                }
                else {
                    $("#editModal").modal('hide');
                    alert("Error running job or no file to download");
                }

            }
        });
    }
    else if (tool == ".Net-IT Tool") {
        
        $("#loadingDiv").removeClass("hidden");
        $('.modal-body, .modal-footer').each(function (i, div) {
            div.className += ' hidden'
        });
        console.log("RunJobAdhoc");
        $.ajax({
            url: controllerActions().RunAdHocJob,
            data: { jobIndex: productId, parametersJson: '[""]', launchServerName: 'Sparrow' },
            cache: false,
            type: 'POST',
            success: function (data) {
                ManageDisplays();
                alert("Job Launched Successfully");
            },
            error: function (response) {
                alert("There was a problem launching the job. If this persists please contact the Helpdesk.");                
            }
        });
        JobDetails(productId, true);
    }
    else {
        console.log("LaunchJobToController");
        $.ajax({
            url: controllerActions().LaunchJobToController,
            data: { jobId: productId, tool: tool, runMode: runMode, owner: owner  },
            cache: false,
            success: function (data) {
                if (source == "iFrame") {
                    console.log(data);
                    if (!data) {
                        alert("Something was not right, please try again or call the help desk")
                    }
                } else {
                    $("#editModal").modal('hide');
                    var jobStat = $("#" + productId)[0];
                    $(jobStat).show();
                    getStatus(productId, "#" + productId + "img", "#" + productId + "val", false, false, tool);
                }
                
                
            }
        });
    }

}

function GetJobAttachments(jobId) {
    $('#attachmentsButton').html('<i class="fa-solid fa-circle-notch fa-spin"></i>');
    $.ajax({
        type: "GET",
        url: controllerActions().DownloadAttachments(jobId),
        success: function () {
            window.location = controllerActions().DownloadAttachments(jobId)
            $('#attachmentsButton').html('<i class="fa-solid fa-circle-check"></i>');
            setTimeout(
                function () {
                    $('#attachmentsButton').html('Get Attachments');
                }, 3000);
        },
        error: function () {
            $('#attachmentsButton').html('<i class="fa-solid fa-hexagon-exclamation"></i>');
        }
        
    })
    
}

function filter() {
    
    var table = document.getElementById("jobTable");
    var r = 2;
    var anyMatch = false;
    if (table != null) {
        while (row = table.rows[r++]) {
            var inner = (row.id).toString().toUpperCase();
            var val = (document.getElementById("searchfield").value).toString().toUpperCase();
            if (inner.match(val) == null) {
                row.style.display = 'none';
            }
            else {
                row.style.display = 'table-row';
                anyMatch = true;
                
            }
        }
        if (!anyMatch) {
            table.rows[1].style.display = 'table-row';
        }
        else {
            table.rows[1].style.display = 'none';
        }
    }
}

function launchHelp() {
    $("a").each(function () {
        $(this).css('pointer-events', 'none');
    });
    $.ajax({
        url: controllerActions().HelpPage,
        success: function (data) {
            $('#helpModalWrapper').html(data);
            $('#helpModal').modal();
            $("a").each(function () {
                $(this).css('pointer-events', '');
            });
        }
    });
}


function getWebRequestData(caller, method) {
    $(caller).select2({
        width: "100%", // Needed for IE support
        allowClear: true,
        placeholder: "Select an option...",
        ajax: {
            url: controllerActions().WebRequestListData,
            dataType: "json",
            delay: 250,
            data: function (params) {
                return {
                    term: params.term,
                    listIdentifier: method,
                    page: params.page || 1
                };
            },
            processResults: function (data, params) {
                params.page = params.page || 1;
                return {
                    results: data.results,
                    pagination: {
                        more: (params.page * 100) < data.totalCount
                    }
                };
            },
            cache: false
        }
    });
}

function isInViewport(element) {
    const rect = element.getBoundingClientRect();
    return (
        rect.top >= 0 &&
        rect.left >= 0 &&
        rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) &&
        rect.right <= (window.innerWidth || document.documentElement.clientWidth)
    );
}

function ManageDisplays() {
    $('.loading').modal('hide');
    $("#editModal").modal('hide');
    $('.modal-backdrop').remove();
}
