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
        'luegg.directives',
        'permission',
        'permission.ui',
        'ngWebSocket'
        // 'localytics.directives'
        // 'AdalAngular'
    ]);


    app.run(['$templateCache', '$rootScope', '$state', '$stateParams', 'dataservice', 'PermPermissionStore', function ($templateCache, $rootScope, $state, $stateParams, dataservice, PermPermissionStore) {
       $rootScope.$state = $state;
        PermPermissionStore
            .definePermission('isAuthorized', function () {
                return false;
            });

        // PermPermissionStore.defineRole('AUTH', ['listEvents', 'editEvents']);
        // var role = PermRoleStore.getRoleDefinition('AUTH');

       dataservice.getData().then(function (data) {
         $rootScope.mdata = data;
         $rootScope.search = $rootScope.mdata.accounts;
        });
    }]);


})();
