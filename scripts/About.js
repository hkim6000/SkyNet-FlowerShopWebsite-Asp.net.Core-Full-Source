var AboutJs = (function () {

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
        $ApiRequest('About/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('ab-toast');
        t.textContent = msg;
        t.classList.add('ab-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ab-open');
        }, 3200);
    }

    function openNav() {
        el('ab-nav').classList.add('ab-open');
        el('ab-scrim').classList.add('ab-open');
    }

    function closeNav() {
        el('ab-nav').classList.remove('ab-open');
        el('ab-scrim').classList.remove('ab-open');
    }

    function search(value) {
        var v = (value || '').trim();
        clearTimeout(timer);
        if (v.length < 2) {
            el('ab-sugg').classList.remove('ab-open');
            return;
        }
        timer = setTimeout(function () {
            call('Suggest', [{ key: 'typed', vlu: v }]);
        }, 250);
    }

    function openSugg() {
        el('ab-sugg').classList.add('ab-open');
    }

    function go() {
        var v = val('ab-q');
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
        var b = el('ab-cartn');
        b.textContent = n;
        b.classList.toggle('ab-has', n > 0);
    }

    function money(v) {
        return '$' + Number(v).toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    }



    function reveal() {
        paintCart();
        document.addEventListener('click', function (e) {
            var s = el('ab-sugg');
            if (s && !s.parentElement.contains(e.target)) {
                s.classList.remove('ab-open');
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
    document.addEventListener('DOMContentLoaded', AboutJs.reveal);
} else {
    AboutJs.reveal();
}
