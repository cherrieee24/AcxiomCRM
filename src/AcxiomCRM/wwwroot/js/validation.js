// Client-side half of NotInPastAttribute (Infrastructure/NotInPastAttribute.cs).
// Rejects dates before today unless a sibling field holds one of the "unless" values
// (e.g. an opportunity whose Stage is Won/Lost may keep a past close date).
(function ($) {
    if (!$.validator || !$.validator.unobtrusive) return;

    // Use the spec's wording for the built-in rules triggered by input types.
    $.extend($.validator.messages, {
        email: "Enter a valid email address.",
        number: "Enter a valid number.",
        date: "Enter a valid date."
    });

    $.validator.addMethod("notpast", function (value, element, params) {
        if (!value) return true;

        if (params.other && params.values) {
            var prefix = element.name.lastIndexOf(".") >= 0 ? element.name.substr(0, element.name.lastIndexOf(".") + 1) : "";
            var other = $(element.form).find("[name='" + prefix + params.other + "']").val();
            if (params.values.split(",").indexOf(other) >= 0) return true;
        }

        var parts = value.substring(0, 10).split("-");
        if (parts.length !== 3) return true; // let the date-format rule report it
        var picked = new Date(+parts[0], +parts[1] - 1, +parts[2]);
        var today = new Date();
        today.setHours(0, 0, 0, 0);
        return picked >= today;
    });

    $.validator.unobtrusive.adapters.add("notpast", ["other", "values"], function (options) {
        options.rules.notpast = { other: options.params.other, values: options.params.values };
        options.messages.notpast = options.message;
    });

    // Re-check the close date when the stage changes.
    $(document).on("change", "select[name$='Stage']", function () {
        var $date = $(this.form).find("[data-val-notpast]");
        if ($date.length && $date.val()) $date.valid();
    });
})(jQuery);
