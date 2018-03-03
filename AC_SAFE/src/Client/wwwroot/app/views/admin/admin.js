(function () {
    'use strict';
    var controllerId = 'admin';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', '$scope', '$uibModal', '$rootScope', admin]);

    function admin(common, dataservice, $scope, $uibModal, $rootScope) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;

        $scope.openModal = function () {
            var m = new Mnemonic(96);
            $rootScope.modal = {};
            $rootScope.modal.password = m.toWords().join(' ');;
            $rootScope.modal.hexPass = m.toHex();
            $rootScope.modal.guid = dataservice.getId();
            $rootScope.modal.ok =function () {
                dataservice.newAccount().then(function (data) {
                    $rootScope.$emit('updateAccounts');
                });
            };
            var modalInstance = $uibModal.open({
                templateUrl: '/app/views/accounts/create_account.html',
                controller: modalCtrl
            });
        };

        activate();

        function activate() {
            common.activateController(controllerId)
                .then(function () { log('Activated Admin View') });//log('Activated Admin View');
        }





    };


})();
