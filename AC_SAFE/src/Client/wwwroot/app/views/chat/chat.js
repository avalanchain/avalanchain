
(function() {
    'use strict';
    var controllerId = 'chat';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', '$scope', '$filter', '$uibModal', '$rootScope', '$stateParams', chat]);

    function chat(common, dataservice, $scope, $filter, $uibModal, $rootScope, $stateParams) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;

        dataservice.getData().then(function(data) {
            vm.chat = data.chat;
            vm.messages = vm.chat.messages;
            vm.users = vm.chat.users;
            vm.lastMessage = new Date();
            vm.lastMessage = vm.chat.lastMessage
        });

        vm.send = function(message) {
            if (message.length > 0) {
                vm.messages.push({
                    id: dataservice.getId(),
                    name: vm.users[0].name,
                    date: new Date(),
                    message: message,
                    side: 'right',

                })
            }
            vm.message = '';
            vm.lastMessage = new Date();
        };



        activate();

        function activate() {
            common.activateController([], controllerId)
                .then(function() {
                    //log('Activated Chat');
                }); //log('Activated Admin View');
        }

    };


})();
