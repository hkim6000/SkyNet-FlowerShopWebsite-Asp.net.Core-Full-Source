var ShopJs = (function () {

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
        $ApiRequest('Shop/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('sh-toast');
        t.textContent = msg;
        t.classList.add('sh-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('sh-open');
        }, 3200);
    }

    function openNav() {
        el('sh-nav').classList.add('sh-open');
        el('sh-scrim').classList.add('sh-open');
    }

    function closeNav() {
        el('sh-nav').classList.remove('sh-open');
        el('sh-scrim').classList.remove('sh-open');
    }

    function search(value) {
        var v = (value || '').trim();
        clearTimeout(timer);
        if (v.length < 2) {
            el('sh-sugg').classList.remove('sh-open');
            return;
        }
        timer = setTimeout(function () {
            call('Suggest', [{ key: 'typed', vlu: v }]);
        }, 250);
    }

    function openSugg() {
        el('sh-sugg').classList.add('sh-open');
    }

    function go() {
        var v = val('sh-q');
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
        var b = el('sh-cartn');
        b.textContent = n;
        b.classList.toggle('sh-has', n > 0);
    }

    function money(v) {
        return '$' + Number(v).toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    }

    function pick(occ) {
        el('sh-occ').value = occ;
        filter();
    }

    function filter() {
        call('List', [
            { key: 'occ', vlu: val('sh-occ') },
            { key: 'pr', vlu: val('sh-price') },
            { key: 'ord', vlu: val('sh-sort') },
            { key: 'find', vlu: val('sh-find') }
        ]);
    }

    function clear() {
        el('sh-find').value = '';
        el('sh-occ').value = '';
        el('sh-price').value = '';
        el('sh-sort').value = 'new';
        filter();
    }

    function filtered(occ, pr, ord, find) {
        var p = new URLSearchParams();
        if (occ) {
            p.set('o', occ);
        }
        if (pr) {
            p.set('p', pr);
        }
        if (ord && ord !== 'new') {
            p.set('s', ord);
        }
        if (find) {
            p.set('term', find);
        }
        var qs = p.toString();
        history.replaceState(null, '', 'Shop' + (qs ? '?' + qs : ''));
    }

    function reveal() {
        paintCart();
        document.addEventListener('click', function (e) {
            var s = el('sh-sugg');
            if (s && !s.parentElement.contains(e.target)) {
                s.classList.remove('sh-open');
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
        pick: pick,
        filter: filter,
        clear: clear,
        filtered: filtered
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ShopJs.reveal);
} else {
    ShopJs.reveal();
}
