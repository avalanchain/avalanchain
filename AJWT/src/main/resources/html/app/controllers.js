/**
 * avalanchain - Responsive Admin Theme
 * Copyright 2015 Webapplayers.com
 *
 */

/**
 * MainCtrl - controller
 */
function MainCtrl() {

    this.userName = 'User';
    this.helloText = 'Welcome in Avalanchain';
    this.descriptionText = 'CASCADING REACTIVE BLOCKCHAINS';

};

function loginCtrl($scope, PermPermissionStore, $state) {

    var vm = this;

    vm.loginForm = function (user) {
        if (vm.login.$valid) {
            PermPermissionStore
                .definePermission('isAuthorized', function () {
                    return true;
                });
            $state.go('index.admin');
        } else {
            vm.login.submitted = true;
        }
    }

};

function modalCtrl($scope, $uibModalInstance, dataservice, $rootScope, common) {
    $scope.modal = $rootScope.modal;
    $scope.ok = function () {
        //common.spinnerTogle(true);
        $scope.modal.ok().then(function (status) {
            if (status === 200)
                $uibModalInstance.close();
        });
        ;
    };

    $scope.cancel = function () {
        $scope.modal.cancel();
        $uibModalInstance.dismiss('cancel');
    };

    $scope.delete = function () {
        $scope.modal.delete();
        $uibModalInstance.dismiss('cancel');
    };
};


angular
    .module('avalanchain')
    .controller('MainCtrl', MainCtrl)
    .controller('loginCtrl', loginCtrl)
    .controller('modalCtrl', MainCtrl);