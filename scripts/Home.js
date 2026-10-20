var HomeJs = (function () {

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
        $ApiRequest('Home/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('hm-toast');
        t.textContent = msg;
        t.classList.add('hm-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('hm-open');
        }, 3200);
    }

    function openNav() {
        el('hm-nav').classList.add('hm-open');
        el('hm-scrim').classList.add('hm-open');
    }

    function closeNav() {
        el('hm-nav').classList.remove('hm-open');
        el('hm-scrim').classList.remove('hm-open');
    }

    function search(value) {
        var v = (value || '').trim();
        clearTimeout(timer);
        if (v.length < 2) {
            el('hm-sugg').classList.remove('hm-open');
            return;
        }
        timer = setTimeout(function () {
            call('Suggest', [{ key: 'typed', vlu: v }]);
        }, 250);
    }

    function openSugg() {
        el('hm-sugg').classList.add('hm-open');
    }

    function go() {
        var v = val('hm-q');
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
        var b = el('hm-cartn');
        b.textContent = n;
        b.classList.toggle('hm-has', n > 0);
    }

    function money(v) {
        return '$' + Number(v).toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    }



    function reveal() {
        paintCart();
        document.addEventListener('click', function (e) {
            var s = el('hm-sugg');
            if (s && !s.parentElement.contains(e.target)) {
                s.classList.remove('hm-open');
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

    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', HomeJs.reveal);
} else {
    HomeJs.reveal();
}
