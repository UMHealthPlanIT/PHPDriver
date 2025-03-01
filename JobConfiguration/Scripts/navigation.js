function displayNavigation(options) {
    var html = '<a href="';

    if (typeof options.brandingLink !== "undefined" && typeof options.brandingLink.url !== "undefined") {
        html += options.brandingLink.url;
    }
    else {
        html += '/';
    }

    html += '"class="svg"><img src=/Content/images/PHP_logo_white.png class="top-logo"/></a>';

    $("#logo-div").html(html);
    var oldhtml = html;
    html = '<ul class="nav navbar-nav" id="thelinks">';
    if (typeof options.links !== "undefined" && options.links.length > 0) {
        options.links.forEach(function (value, index) {
            html += "<li><a href=\"/" + options.links[index].controllerName + "/" + options.links[index].actionName;
            if (typeof options.links[index].parameters !== "undefined" && options.links[index].parameters.length > 0) {
                options.links[index].parameters.forEach(function (value, paramIndex) {
                    if (paramIndex == 0) {
                        html += "?";
                    }
                    else {
                        html += "&";
                    }
                    html += options.links[index].parameters[paramIndex].name + "=" + options.links[index].parameters[paramIndex].value;
                });
            }
            html += "\">";
            if (typeof options.links[index].icon !== "undefined") {
                html += "<i class=\"fa " + options.links[index].icon + "\"></i>&nbsp;";
            }
            html += options.links[index].linkText + "</a></li>";
        });
    }
    html += "</ul>";
    $("#links-div").html(html);
    html = "";
    if (typeof options.search !== "undefined" && options.search.enabled == true) {
        $("#search-div").addClass("pull-right");
        if (options.search.enabled == true) {
            html += '<form class="navbar-form form-wrapper"';
            if (options.search.method == "POST") {
                html += ' action="' + options.search.postAction + '" method="POST"';
            }
            html += '>';
            html += '<input type="text" class="form-control glow-brand-color-secondary" id="navigation-search-bar" name="navigation-search-bar" placeholder="Search"';
            if (options.search.method == "JavaScript") {
                html += " onkeyup=\"" + options.search.javascriptAction + "\"";
            }
            html += '/>';
            if (options.search.method == "POST") {
                html += '<button type="submit" class="button-sparrow">Search</button></form>';
            }
        }
    }
    var currentHtml = $("#search-div").html();
    $("#search-div").html(html + currentHtml);
    if (oldhtml.indexOf("sparrowlogosmall.png") > 0) {
        //$("#thelinks").addClass("imageLinkCorrection");
    }
}