function doThePlumage() {
    $(".dataTables_filter > label > input").addClass("form-control form-control-inline");

    $(".sparrow-spinner").html("<div></div><div></div><div></div><div></div><div></div><div></div><div></div><div></div><div></div><div></div><div></div><div></div>");

    // Check to see if the browser supports input type="date"...IE does not.
    // Use jquery.ui datepicker for IE.
    if (!browserSupportsDateInput()) {
        $("input[type='date']").datepicker();
    }
}

function browserSupportsDateInput() {
    var i = document.createElement("input");
    i.setAttribute("type", "date");
    return i.type !== "text";
}

function toastifyNoJam() {
    var content = $(".sp-toast").html();

    $(".sp-toast").addClass("well");
    $(".sp-toast").html('<div class="sp-toast-main"></div><div class="sp-toast-indicator"><span class="glyphicon glyphicon-option-vertical"></span></div>');
    $(".sp-toast-main").html(content);
}

function progbarInitialize(options) {
    var progbar = $(".sparrow-progbar");

    if (progbar.length > 0) {
        var numSteps = options.steps.length;
        var stepWidth = 100 / numSteps;
        var newHtml = "";

        for (var i = 0; i < numSteps; i++) {
            newHtml += '<div class="sparrow-progbar-step" style="width: ' + stepWidth + '%">' +
                '<input class="sparrow-progbar-desc" type="hidden" value="' + options.steps[i].stepDescription + '" />' +
                '<input class="sparrow-progbar-desc-title" type="hidden" value="' + options.steps[i].stepTitle + '" />' +
                '<div class="sparrow-progbar-step-title">' + options.steps[i].stepTitle + '</div>' +
                '<div class="sparrow-progbar-dot"></div>' +
                '<div class="sparrow-progbar-staticbar"></div>' +
                '<div class="sparrow-progbar-bar"></div>' +
                '</div>';
        }

        $('<input id="progressBarStep" name="progressBarStep" type="hidden" value="0">').insertBefore(progbar);

        if (options.stepDescriptionDisplay) {
            $('<div id="progBarDesc" class="sparrow-progbar-description"><div class="well"><h4>' + options.steps[0].stepTitle + '</h4>' +
                options.steps[0].stepDescription + '</div ></div >').insertAfter(progbar);
        }

        progbar.html(newHtml);
        progbarSetStep($("#progressBarStep").val());

        $("#" + options.stepNavigation.next).click(function () {
            var currentStep = 1 * $("#progressBarStep").val();
            progbarIncrementStep(currentStep);
        });

        if ($("#" + options.stepNavigation.previous).length > 0) {
            $("#" + options.stepNavigation.previous).click(function () {
                var currentStep = 1 * $("#progressBarStep").val();
                progbarDecrementStep(currentStep);
            })
        }
    }
}

function progbarSetStep(step) {
    var steps = $(".sparrow-progbar-step");

    for (var i = 0; i < steps.length; i++) {
        $(steps[i]).removeClass("pb-active pb-disabled");
        if (i == step) {
            $(steps[i]).addClass("pb-active");
        }
        else if (i > step) {
            $(steps[i]).addClass("pb-disabled");
        }
    }
}

function progbarIncrementStep(currentStep) {
    var steps = $(".sparrow-progbar-step");
    var showDesc = $("#progBarDesc").length > 0;

    if (currentStep < steps.length - 1) {
        $("#progressBarStep").val(currentStep + 1);
        $(steps[currentStep]).removeClass("pb-active");
        $(steps[currentStep + 1]).removeClass("pb-disabled").addClass("pb-active");

        if (showDesc) {
            var title = $($(steps[currentStep + 1]).find('input[class="sparrow-progbar-desc-title"]')).val();
            var stepDescText = $($(steps[currentStep + 1]).find('input[class="sparrow-progbar-desc"]')).val();
            $("#progBarDesc > .well").html('<h4>' + title + '</h4>' + stepDescText);
        }
    }
}

function progbarDecrementStep(currentStep) {
    var steps = $(".sparrow-progbar-step");
    var showDesc = $("#progBarDesc").length > 0;

    if (currentStep > 0) {
        $("#progressBarStep").val(currentStep - 1);
        $(steps[currentStep]).removeClass("pb-active").addClass("pb-disabled");
        $(steps[currentStep - 1]).addClass("pb-active");

        if (showDesc) {
            var title = $($(steps[currentStep - 1]).find('input[class="sparrow-progbar-desc-title"]')).val();
            var stepDescText = $($(steps[currentStep - 1]).find('input[class="sparrow-progbar-desc"]')).val();
            $("#progBarDesc > .well").html('<h4>' + title + '</h4>' + stepDescText);
        }
    }
}