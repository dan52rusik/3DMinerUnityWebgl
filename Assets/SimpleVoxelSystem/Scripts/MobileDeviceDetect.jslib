mergeInto(LibraryManager.library, {
    SVS_IsMobileBrowser: function () {
        try {
            var nav = (typeof navigator !== 'undefined' && navigator) ? navigator : {};
            var ua = String(nav.userAgent || '');
            var platform = String(nav.platform || '');
            var maxTouchPoints = Number(nav.maxTouchPoints || 0);

            var mobileUA = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini|Windows Phone|Mobile/i.test(ua);
            var iPadLike = /Mac/i.test(platform) && maxTouchPoints > 1;
            var coarse = false;
            try {
                coarse = !!(typeof window !== 'undefined' && window.matchMedia && window.matchMedia('(pointer: coarse)').matches);
            } catch (e) {}

            if (mobileUA || iPadLike)
                return 1;

            if (maxTouchPoints > 1 && coarse)
                return 1;

            return 0;
        } catch (e) {
            return 0;
        }
    }
});
