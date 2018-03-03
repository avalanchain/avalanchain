(function () {
    'use strict';
    var controllerId = 'assets';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', '$scope', '$uibModal', '$rootScope', '$state', assets]);

    function assets(common, dataservice, $scope, $uibModal, $rootScope, $state) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        vm.info = 'Assets';

        dataservice.getData().then(function(data) {
            vm.assets = data.assets;
        });

        activate();

        function activate() {
            common.activateController([], controllerId)
                .then(function () {
                    log('Activated Assets');
                }); //log('Activated Admin View');
        }
    };


})();