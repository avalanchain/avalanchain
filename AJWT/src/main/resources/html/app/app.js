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
        'ncy-angular-breadcrumb',
        'irontec.simpleChat',
        'luegg.directives'
        // 'localytics.directives'
        // 'AdalAngular'
    ]);


    app.run(['$templateCache', '$rootScope', '$state', '$stateParams', 'dataservice', function ($templateCache, $rootScope, $state, $stateParams, dataservice) {
       $rootScope.$state = $state;
       dataservice.getData().then(function (data) {
         $rootScope.mdata = data;
         $rootScope.search = $rootScope.mdata.accounts;
        });
    }]);


})();
