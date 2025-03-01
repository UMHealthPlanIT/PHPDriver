$(document).ready(function () {


});

function initializeProgBar(curStep) {
    var steps = $('.jc-progbar > .jc-progbar-step');
    var numSteps = steps.length;

    var curStepIndex = curStep - 1;
    var i = 0;
    for (i; i < numSteps; i++) {

        $(steps[i]).removeClass('jc-disabled jc-active');

        if (i == curStepIndex) {
            $(steps[i]).addClass('jc-active');
        } else if (i > curStepIndex) {
            $(steps[i]).addClass('jc-disabled');
        }
    }
}