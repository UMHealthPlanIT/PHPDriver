function ShowHideInfo() {

    if ($("#icon").val() == "unclicked") {
        $("#info").hide();
        $("#icon").val("clicked");
    }
    else {
        $("#info").show();
        $("#icon").val("unclicked");
    }
};