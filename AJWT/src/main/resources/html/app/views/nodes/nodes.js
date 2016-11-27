(function() {
    'use strict';
    var controllerId = 'Nodes';
    angular.module('avalanchain').controller(controllerId, ['common','dataservice','$state', Nodes]);

    function Nodes(common, dataservice, $state) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);

        var vm = this;
        dataservice.getNodes(vm);
        vm.showNode = function(node) {
            $state.go('index.node', {
                nodeId: node.id
            });
        }

        vm.addNode = function () {
            dataservice.addNode().then(function(data) {
                var dt = data.data;
            });

        }

        activate();

        function activate() {
            common.activateController([getData(vm)], controllerId)
                .then(function() {
                    log('Activated Nodes')
                }); //log('Activated Admin View');
        }

        function getData(vm) {
          // dataservice.getData().then(function(data) {
          //   vm.data = data;
          //   vm.nodes = vm.data.nodes;
          // });

        }

    };


})();
