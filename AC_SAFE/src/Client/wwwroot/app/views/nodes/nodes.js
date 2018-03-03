(function() {
    'use strict';
    var controllerId = 'Nodes';
    angular.module('avalanchain').controller(controllerId, ['common','dataservice','$state', Nodes]);

    function Nodes(common, dataservice, $state) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);

        var vm = this;

        vm.showNode = function(node) {
            $state.go('index.node', {
                nodeId: node.id
            });
        }

        activate();

        function activate() {
            common.activateController([getData()], controllerId)
                .then(function() {
                    log('Activated Nodes')
                }); //log('Activated Admin View');
        }

        function getData() {
          dataservice.getData().then(function(data) {
            vm.data = data;
            vm.nodes = vm.data.nodes;
          });

        }
    };


})();
