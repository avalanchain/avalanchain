(function () {
    'use strict';
    var controllerId = 'admin';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', '$scope', admin]);

    function admin(common, dataservice, $scope) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        vm.info = 'accounts';
        vm.helloText = 'Welcome in Avalanchain';
        vm.descriptionText = 'CASCADING REACTIVE BLOCKCHAINS';



        activate();

        function activate() {
            common.activateController([getMessageCount()], controllerId)
                .then(function () { log('Activated Admin View') });//log('Activated Admin View');
        }





    };


})();
