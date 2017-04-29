(function () {
    "use strict";


    var animate = function (span) {
        var $span = $(span);
        $span.animate({
            opacity: 0,
            top: "-40px"
        }, "slow", "swing", function () {
            $span.remove();
        });
    }
    var showAdd = function (pos) {
        var span = document.createElement("span");
        span.innerText = "+1";
        if (pos == "left")
            $(".container.left > .float")[0].appendChild(span);
        else if (pos == "right")
            $(".container.right > .float")[0].appendChild(span);
        animate(span);
    }


    var socket = undefined;
    var initsocket = function () {
        if (socket != undefined)
            return;

        socket = new WebSocket("ws://" + location.host + "/game/socket");
        socket.onclose = function () {

        };
        socket.onopen = function () {

        };
        socket.onmessage = function (evt) {
            var msg = eval("(" + evt.data + ")");
            switch (msg.type) {
                case "add":
                    showAdd(msg.side);
                    $(".container.left > span.count")[0].innerText = msg.left;
                    $(".container.right > span.count")[0].innerText = msg.right;
                    var pct = 0.5;
                    if (msg.left + msg.right != 0)
                        pct = msg.left * 1.0 / (msg.left + msg.right);
                    $(".progress")[0].style.width = (pct * 100) + "%";
                    break;
                case "set":
                    $(".center-content > .title")[0].innerText = msg.title;
                    $(".container.left > img")[0].src = msg.leftimg;
                    $(".container.right > img")[0].src = msg.rightimg;
                    $(".container.left > span.count")[0].innerText = msg.left;
                    $(".container.right > span.count")[0].innerText = msg.right;
                    var pct = 0.5;
                    if (msg.left + msg.right != 0)
                        pct = msg.left * 1.0 / (msg.left + msg.right);
                    $(".progress")[0].style.width = (pct * 100) + "%";
                    break;
                case "disableedit":
                    $("#btn_edit")[0].context.set_enabled(false);
                    break;
                case "enableedit":
                    $("#btn_edit")[0].context.set_enabled(true);
                    break;
                case "change":
                    break;
                case "resettimer":
                    $(".center-content > .title")[0].innerText = "Board changed in " + msg.val + "s";
                    break;
            }
        };
        socket.onerror = function () {

        };
    }

    var sendadd = function (side) {
        if (socket == null)
            return;

        if (side == "left")
            socket.send("/add left");
        else if (side == "right")
            socket.send("/add right");
    }

    var submittoken = undefined;
    var load = function () {
        $(".container.left > img").click(function () {
            sendadd("left");
        });
        $(".container.right > img").click(function () {
            sendadd("right");
        });

        $("input[name='leftpic']").change(function () {
            var windowURL = window.URL || window.webkitURL;
            if (this.files.length <= 0) {
                var dataurl = "";
                $(".set-left > img")[0].src = "";
            }
            else {
                var dataurl = windowURL.createObjectURL(this.files[0]);
                $(".set-left > img")[0].src = dataurl;
            }
        });
        $("input[name='rightpic']").change(function () {
            var windowURL = window.URL || window.webkitURL;
            if (this.files.length <= 0) {
                var dataurl = "";
                $(".set-right > img")[0].src = "";
            }
            else {
                var dataurl = windowURL.createObjectURL(this.files[0]);
                $(".set-right > img")[0].src = dataurl;
            }
        });
        $("#btn_edit")[0].context.events.addHandler("click", function () {
            $.post("/game/requestedit", {}, function (ret) {
                if (ret.can == false) return;

                $("input[name='title']").val('');
                $("input[name='leftpic']").val('');
                $("input[name='rightpic']").val('');
                $(".set-left > img")[0].src = "";
                $(".set-right > img")[0].src = "";
                $("#error_hint")[0].style.opacity = 0;
                $("#settings")[0].style.display = "flex";
            });

        });
        $("#btn_leftpic")[0].context.events.addHandler("click", function () {
            $("input[name='leftpic']")[0].click();
        });
        $("#btn_rightpic")[0].context.events.addHandler("click", function () {
            $("input[name='rightpic']")[0].click();
        });
        $("#btn_cancel")[0].context.events.addHandler("click", function () {
            $.post("/game/canceledit", {}, function () {
                $("#settings")[0].style.display = "none";
            });

        });

        $("#btn_submit")[0].context.events.addHandler("click", function () {
            $("#submit-form").ajaxSubmit({
                url: "/game/submit",
                method: "post",
                success: function (ret) {
                    if (ret.result == true) {
                        $("#settings")[0].style.display = "none";
                    }
                    else {
                        $("#error_hint")[0].innerText = "数据错误";
                        $("#error_hint")[0].style.opacity = 1;
                    }

                    return false;
                }
            });
        });
        initsocket();
    };

    window.addEventListener("load", load, false);

})();