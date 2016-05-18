/**
 * avalanchain - Responsive Admin Theme
 * Copyright 2015 Webapplayers.com
 *
 */
(function () {
    var app = angular.module('avalanchain', [
        'ui.router',                    // Routing
        'oc.lazyLoad',                  // ocLazyLoad
        'ui.bootstrap',                 // Ui Bootstrap
        'common',
        'monospaced.qrcode',
        'ngWebSocket'
    ]);


    app.run(['$templateCache', '$rootScope', '$state', '$stateParams', 'dataservice', function ($templateCache, $rootScope, $state, $stateParams, dataservice) {
        // dataservice.getAllAccounts().then(function (data) {
        //     $rootScope.accountsamount = data.data.length;
        // });
        dataservice.getNodes($rootScope);
    }]);


})();

