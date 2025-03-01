// LieutenantCommanderData
//PROGRAMCODE field validation

$(document).ready(function () {
    $("#DelimiterField").value = "";
    $("#DelimiterField").prop('readonly', true);

});

function PCFormatValidation(id) {

    if (((id === "ProgCodeField" && $("#ProgCodeField").val().length < 7) || (id === "ProgCodeField" && $("#ProgCodeField").val().length > 8)) && (id === "ProgCodeField" && $("#ProgCodeField").val().length !== 0)) {
        alert("The program code must be in one of the folowing formats:\r\n\r\nXX99999 or XX99999x\r\nXXX9999\r\nXX_9999 or XX_9999x\r\n\r\n(X=Upper Case Character, x=Lower Case Character, 9=Number)\r\nThe end lower-case character is optional, 7 or 8 characters in all.");
        document.getElementById("ProgCodeField").focus();
        $("#ProgCodeField").val('');
        
    }


    if (id === "ProgCodeField" && $("#ProgCodeField").val().length === 7) {
        var re1 = /[A-Z]+[A-Z]+[A-Z0-9]+[0-9]+[0-9]+[0-9]+[0-9]/; //Test for standard format
        var isProcCode1 = re1.test($("#ProgCodeField").val());
        if (!isProcCode1) {
            var re2 = /[A-Z]+[A-Z]+[_]+[0-9]+[0-9]+[0-9]+[0-9]/; //Test for legacy format
            var isProcCode2 = re2.test($("#ProgCodeField").val());
            if (!isProcCode2) {
                alert("The program code must be in one of the folowing formats:\r\n\r\nXX99999 or XX99999x\r\nXXX9999\r\nXX_9999 or XX_9999x\r\n\r\n(X=Upper Case Character, x=Lower Case Character, 9=Number)\r\nThe end lower-case character is optional, 7 or 8 characters in all.");
                document.getElementById("ProgCodeField").focus();
                $("#ProgCodeField").val('');
            }
        }
    }
    else if (id === "ProgCodeField" && $("#ProgCodeField").val().length === 8) {
        var re3 = /[A-Z]+[A-Z]+[A-Z0-9]+[0-9]+[0-9]+[0-9]+[0-9]+[a-z]/;
        var isProcCode3 = re3.test($("#ProgCodeField").val());
        if (!isProcCode3) {
            var re4 = /[A-Z]+[A-Z]+[_]+[0-9]+[0-9]+[0-9]+[0-9]+[a-z]/;
            var isProcCode4 = re4.test($("#ProgCodeField").val());
            if (!isProcCode4) {
                alert("The program code must be in one of the folowing formats:\r\n\r\nXX99999 or XX99999x\r\nXXX9999\r\nXX_9999 or XX_9999x\r\n\r\n(X=Upper Case Character, x=Lower Case Character, 9=Number)\r\nThe end lower-case character is optional, 7 or 8 characters in all.");
                document.getElementById("ProgCodeField").focus();
                $("#ProgCodeField").val('');
            }
        }
    }
}

//DELIMITER field validation
function FieldValidation(id) {
    if (id === "DelimiterField" && $("#DelimiterField").val().length > 2) {
        alert("Delimiter can not be more than two characters.");
        document.getElementById("DelimiterField").focus();
        $("#ProgCodeField").val('');
    }
    else if (id === "DelimiterField" && $("#DelimiterField").val().length === 2) {
        if ($("#DelimiterField").val().toString().substr(0, 1) !== "\"") {
            alert("First delimiter must be a double quote to encase each data element of a two character delimiter.");
            document.getElementById("DelimiterField").focus();
            $("#ProgCodeField").val('');
        }
        else if ($("#DelimiterField").val().toString().substr(0, 1) === "\"" && $("#DelimiterField").val().toString().substr(1, 2) === "\"") {
            alert("Are you sure you want both the data encasement character and the delimiter to be double quotes?");
        }
    }

}

//To prevent anything from being put into the CODE field and the STOREDPROCEDURE field at the same time
function DisableUnusedField(id) {
    if (id === "CodeField" && $("#CodeField").val().length > 0) {
        $("#SPField").val("");
        $("#SPField").prop('readonly', true);
        $("#TFSField").val("");
        $("#TFSField").attr('disabled', true); //you cannot mark select drop-downs as read-only, only disable them
        $("#TFSCodeHidden").attr('disabled', false);
    }
    else if (id === "CodeField" && $("#CodeField").val().length === 0 && $("#SPField").val().length === 0 && $("#TFSField").val().length === 0) {
        $("#SPField").prop('readonly', false);
        $("#TFSField").attr('disabled', false);
        $("#TFSCodeHidden").attr('disabled', true);

    }
    else if (id === "SPField" && $("#SPField").val().length > 0) {
        $("#CodeField").val("");
        $("#CodeField").prop('readonly', true);
        $("#TFSField").val("");
        $("#TFSField").attr('disabled', true);
        $("#TFSCodeHidden").attr('disabled', false);
    }
    else if (id === "SPField" && $("#CodeField").val().length === 0 && $("#SPField").val().length === 0 && $("#TFSField").val().length === 0) {
        $("#CodeField").prop('readonly', false);
        $("#TFSField").attr('disabled', false);
        $("#TFSCodeHidden").attr('disabled', true);
    }
    else if (id === "TFSField" && $("#TFSField").val().length > 0) {
        $("#CodeField").val("");
        $("#CodeField").prop('readonly', true);
        $("#SPField").val("");
        $("#SPField").prop('readonly', true);
        $("#TFSCodeHidden").attr('disabled', true);
    }
    else if (id === "TFSField" && $("#CodeField").val().length === 0 && $("#SPField").val().length === 0 && $("#TFSField").val().length === 0) {
        $("#SPField").prop('readonly', false);
        $("#CodeField").prop('readonly', false);
        $("#TFSCodeHidden").attr('disabled', false);
    }
    else if (id === "OutputFileType" && $("#OutputFileType").val().length > 0) {
        if ($("#OutputFileType").val().toString() === "EXCEL") {
            $("#DelimiterField").prop('readonly', true);
        }
        else if ($("#OutputFileType").val().toString() !== "EXCEL") {
            $("#DelimiterField").prop('readonly', false);
        }
    }
}


//CODE field AND the STOREDPROCEDURE field remider to have the code tested before promotion
//Need approval
//function CodeTestReminder(id) {
//    if ((id === "CodeField" && $("#CodeField").val().length > 0) || (id === "SPField" && $("#SPField").val().length > 0)) {
//        alert("Please verify that this has been thoroughly tested, Thank you.");
//    }

//}
