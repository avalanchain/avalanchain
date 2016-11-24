(function () {
    'use strict';
    var controllerId = 'clusters';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', '$state',  clusters]);

    function clusters(common, dataservice, $state) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        vm.showCluster = function(cluster) {
            $state.go('index.cluster', {
                clusterId: cluster.id
            });
        }


        activate();

        function activate() {
            common.activateController([getData()], controllerId)
                .then(function () { log('Activated Clusters') });//log('Activated Admin View');
        }

        function getData() {
          dataservice.getData().then(function(data) {
            vm.data = data;
            vm.clusters = vm.data.clusters;
          });

        }
    };

})();
