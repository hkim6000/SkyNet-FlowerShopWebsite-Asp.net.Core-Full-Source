var AdminLoginJs = (function () {

    var stage = 'in';

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('AdminLogin/' + method, JSON.stringify(list || []));
    }

    function base() {
        return location.pathname.replace(/[^\/]*$/, '');
    }

    function sign() {
        el('lg-btn').disabled = true;
        el('lg-e-main').textContent = '';
        if (stage === 'in') {
            call('SignIn', [{ key: 'login', vlu: val('lg-user') }, { key: 'secret', vlu: el('lg-pass').value }]);
        } else {
            call('SetPassword', [
                { key: 'login', vlu: val('lg-user') },
                { key: 'secret', vlu: el('lg-pass').value },
                { key: 'fresh', vlu: el('lg-np').value },
                { key: 'again', vlu: el('lg-np2').value }
            ]);
        }
        return false;
    }

    function choose() {
        stage = 'set';
        el('lg-new').hidden = false;
        el('lg-passlab').textContent = 'Current password';
        el('lg-btn').textContent = 'Save and sign in';
        el('lg-btn').disabled = false;
        el('lg-np').focus();
    }

    function failed() {
        el('lg-btn').disabled = false;
    }

    function opened(token) {
        document.cookie = 'fs_tok=' + token + '; path=' + base() + '; max-age=28800; samesite=lax' + (location.protocol === 'https:' ? '; secure' : '');
        location.href = 'Admin';
    }

    function reveal() {
        var u = el('lg-user');
        if (u && u.value) {
            el('lg-btn').focus();
        } else if (u) {
            u.focus();
        }
    }

    return {
        reveal: reveal,
        sign: sign,
        choose: choose,
        failed: failed,
        opened: opened
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminLoginJs.reveal);
} else {
    AdminLoginJs.reveal();
}
