var PayJs = (function () {

    var timer = null;
    var ttimer = null;
    var mem = [];

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Pay/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('py-toast');
        t.textContent = msg;
        t.classList.add('py-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('py-open');
        }, 3200);
    }

    function openNav() {
        el('py-nav').classList.add('py-open');
        el('py-scrim').classList.add('py-open');
    }

    function closeNav() {
        el('py-nav').classList.remove('py-open');
        el('py-scrim').classList.remove('py-open');
    }

    function search(value) {
        var v = (value || '').trim();
        clearTimeout(timer);
        if (v.length < 2) {
            el('py-sugg').classList.remove('py-open');
            return;
        }
        timer = setTimeout(function () {
            call('Suggest', [{ key: 'typed', vlu: v }]);
        }, 250);
    }

    function openSugg() {
        el('py-sugg').classList.add('py-open');
    }

    function go() {
        var v = val('py-q');
        location.href = 'Shop' + (v ? '?term=' + encodeURIComponent(v) : '');
        return false;
    }

    function cartRead() {
        try {
            var raw = localStorage.getItem('pb_cart');
            var list = raw ? JSON.parse(raw) : [];
            mem = Array.isArray(list) ? list : [];
        } catch (e) {
        }
        return mem;
    }

    function cartWrite(list) {
        mem = list;
        try {
            localStorage.setItem('pb_cart', JSON.stringify(list));
        } catch (e) {
        }
        paintCart();
    }

    function paintCart() {
        var n = cartRead().reduce(function (sum, i) {
            return sum + (i.q || 0);
        }, 0);
        var b = el('py-cartn');
        b.textContent = n;
        b.classList.toggle('py-has', n > 0);
    }

    function money(v) {
        return '$' + Number(v).toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    }

    function order() {
        var o = el('py-order');
        return o ? { no: o.getAttribute('data-no'), tk: o.getAttribute('data-tk') } : { no: '', tk: '' };
    }

    function start() {
        var o = order();
        el('py-go').disabled = true;
        $WaitOn();
        call('Start', [{ key: 'no', vlu: o.no }, { key: 'tk', vlu: o.tk }]);
    }

    function check() {
        var o = order();
        call('Check', [{ key: 'no', vlu: o.no }, { key: 'tk', vlu: o.tk }]);
    }

    function go(url) {
        if (/^https:\/\//.test(url)) {
            location.href = url;
        }
    }

    function failed() {
        var b = el('py-go');
        if (b) {
            b.disabled = false;
        }
    }

    function reload() {
        var o = order();
        location.href = 'Pay?o=' + encodeURIComponent(o.no) + '&k=' + encodeURIComponent(o.tk);
    }

    function reveal() {
        paintCart();
        document.addEventListener('click', function (e) {
            var s = el('py-sugg');
            if (s && !s.parentElement.contains(e.target)) {
                s.classList.remove('py-open');
            }
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        search: search,
        openSugg: openSugg,
        go: go,
        start: start,
        check: check,
        go: go,
        failed: failed,
        reload: reload
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', PayJs.reveal);
} else {
    PayJs.reveal();
}
