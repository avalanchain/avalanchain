/// <reference path="create_account.html" />
(function() {
    'use strict';
    var controllerId = 'chat';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', '$scope', '$filter', '$uibModal', '$rootScope', '$stateParams', chat]);

    function chat(common, dataservice, $scope, $filter, $uibModal, $rootScope, $stateParams) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;

        dataservice.getChat(vm);
        vm.users = [];
        vm.users.push({
            id: dataservice.getId(),
            name: 'you'
        });
        vm.users.push({
            id: dataservice.getId(),
            name: 'server'
        });
        vm.send = function(message) {
            if (message.length > 0) {
                dataservice.sendMessage(message).then(function(data) {
                    var mes = data.data.msg;
                    vm.message = '';
                });
            }

        };

        $scope.$on("$destroy", function() {
            vm.removeListener(vm);
        });



        activate();

        function activate() {
            common.activateController([], controllerId)
                .then(function() {
                    //log('Activated Chat');
                }); //log('Activated Admin View');
        }

    };


})();
