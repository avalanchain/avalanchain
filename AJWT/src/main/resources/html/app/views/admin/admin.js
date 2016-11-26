(function () {
    'use strict';
    var controllerId = 'admin';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', '$scope', '$uibModal', '$rootScope', admin]);

    function admin(common, dataservice, $scope, $uibModal, $rootScope) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        //TODO: pagination add to service
        $scope.maxSize = 5;
        $scope.totalItems = [];
        $scope.currentPage = 1;
        $scope.userPage = 1;

        vm.createUser = function () {
            var m = new Mnemonic(96);
            $rootScope.modal = {};
            $rootScope.modal.password = m.toWords().join(' ');
            $rootScope.modal.hexPass = m.toHex();
            $rootScope.modal.guid = dataservice.getId();
            $rootScope.modal.ok =function () {
                return dataservice.newUser().then(function (data) {
                    // $rootScope.$emit('updateAccounts');
                    return 200;
                });
            };
            $rootScope.modal.canel =function () {
                // dataservice.newUser().then(function (data) {
                //     $rootScope.$emit('updateAccounts');
                // });
            };
            var modalInstance = $uibModal.open({
                templateUrl: '/app/views/admin/create_user.html',
                controller: modalCtrl
            });
        };

        vm.editUser = function (user) {
            var m = new Mnemonic(96);
            $rootScope.modal = {};
            $rootScope.modal.user = user;
            $rootScope.modal.ok =function () {
                return dataservice.newUser().then(function (data) {
                    // $rootScope.$emit('updateAccounts');
                    return 200;
                });
            };
            $rootScope.modal.canel =function () {
                // dataservice.newUser().then(function (data) {
                //     $rootScope.$emit('updateAccounts');
                // });
            };
            var modalInstance = $uibModal.open({
                templateUrl: '/app/views/admin/edit_user.html',
                controller: modalCtrl
            });
        };

        activate();

        function activate() {
            common.activateController([getData(vm)],controllerId)
                .then(function () { log('Activated Admin View') });//log('Activated Admin View');
        }
        function getData(vm) {
            dataservice.getUsers().then(function(data) {
                vm.users = data.data;
                // vm.nodes = vm.data.nodes;
            });

        }




    };


})();
