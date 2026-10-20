var TrackJs = (function () {

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
        $ApiRequest('Track/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('tk-toast');
        t.textContent = msg;
        t.classList.add('tk-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('tk-open');
        }, 3200);
    }

    function openNav() {
        el('tk-nav').classList.add('tk-open');
        el('tk-scrim').classList.add('tk-open');
    }

    function closeNav() {
        el('tk-nav').classList.remove('tk-open');
        el('tk-scrim').classList.remove('tk-open');
    }

    function search(value) {
        var v = (value || '').trim();
        clearTimeout(timer);
        if (v.length < 2) {
            el('tk-sugg').classList.remove('tk-open');
            return;
        }
        timer = setTimeout(function () {
            call('Suggest', [{ key: 'typed', vlu: v }]);
        }, 250);
    }

    function openSugg() {
        el('tk-sugg').classList.add('tk-open');
    }

    function go() {
        var v = val('tk-q');
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
        var b = el('tk-cartn');
        b.textContent = n;
        b.classList.toggle('tk-has', n > 0);
    }

    function money(v) {
        return '$' + Number(v).toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    }

    function find() {
        call('Find', [{ key: 'no', vlu: val('tk-no') }, { key: 'mail', vlu: val('tk-mail') }]);
        return false;
    }

    function reveal() {
        paintCart();
        document.addEventListener('click', function (e) {
            var s = el('tk-sugg');
            if (s && !s.parentElement.contains(e.target)) {
                s.classList.remove('tk-open');
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
        find: find
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', TrackJs.reveal);
} else {
    TrackJs.reveal();
}
