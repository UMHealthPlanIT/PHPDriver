$(document).ready(function () {

    $(".theme-switch").attr("href", "/JobCentralControl/SetTheme?theme=Light").html("Light Theme");
    $(".navbar-header").append("<h1 style='color: white; position: absolute; left: 45%;'>QB War Room</h1>");
    $(".navbar-header").append("<div id='clearSearch' class='clear-search'>x</div>");

    $("li.alert").click(function () {
        $("li.alert.active").removeClass("active");
        $(this).addClass("active");
    });

    $("#datepicker").addClass("form-control").css("display", "inline-block").css("width", "50%").css("margin-top", "10px").css("margin-bottom", "10px");

    $("#logView").after(`<div id="dragbar" href="#" style="cursor: grab; font-size: 0px;">
						<div style="display: inline-block; width: calc(50% - 18px); height: 2px; margin: 5px 0px; background-color: rgba(128, 0, 128, .5); box-shadow: grey 0 0 5px;"></div>
						<div style="display: inline-block; width: 36px; height: 10px; margin: 0; background-color: rgba(128, 0, 128, .5); box-shadow: #808080a8 0 0 10px; margin-bottom: 1px; border-radius: 3px;">
							<div style="height: 25%; width: 40%; border-bottom: rgba(128, 0, 128, 1) solid 1px; margin-left: 30%; border-radius: 1px;"></div>
							<div style="height: 25%; width: 40%; border-bottom: rgba(128, 0, 128, 1) solid 1px; margin-left: 30%; border-radius: 1px;"></div>
							<div style="height: 25%; width: 40%; border-bottom: rgba(128, 0, 128, 1) solid 1px; margin-left: 30%; border-radius: 1px;"></div>
						</div>
						<div style="display: inline-block; width: calc(50% - 18px); height: 2px; margin: 5px 0px; background-color: rgba(128, 0, 128, .5); box-shadow: grey 0 0 5px;"></div>
					</div>`);

    /* Search the job list in Today's Schedule with the search bar at top */
    $("#searchfield").attr("onkeyup", "");

    $("#searchfield").keyup(function () {
        var search = $(this).val().toUpperCase();

        $("#todaysSchedule > ul > li").each(function () {
            if ($("a", this).html().includes(search)) {
                $(this).show();
            }
            else {
                $(this).hide();
            }
        });
    });

    $("#clearSearch").click(function () {
        $("#searchfield").val("").keyup();
    });

    /* Everything below is for the panel resize drag-bar. */
    var isMouseDown = false;
    var minHeight = 100;
    var yStartPosition;

    $("#dragbar").mousedown(function (e) {
        isMouseDown = true;
        yStartPosition = e.pageY;
        $("#dragbar").css("cursor", "grabbing");
    });

    $(document).mouseup(function (e) {
        isMouseDown = false;
        $("#dragbar").css("cursor", "grab");
    });

    $(document).mousemove(function (e) {
        if (isMouseDown) {
            var startHeightTop = $("#logView").height();
            var startHeightBot = $("#controlSwitch").height();
            var totalHeight = startHeightTop + startHeightBot;

            var change = yStartPosition - e.pageY;

            // Make sure neight of the divs go over the min height, adjust change if they will.
            if (Math.max(minHeight, startHeightTop - change) == minHeight) {
                change = startHeightTop - minHeight;
                isMouseDown = false;
            }
            else if (Math.max(minHeight, startHeightBot + change) == minHeight) {
                change = startHeightBot - minHeight;
                isMouseDown = false;
            }

            $("#logView").height(startHeightTop - change);
            $("#controlSwitch").height(startHeightBot + change);

            // Reset start position
            yStartPosition = e.pageY;
        }
    });
})