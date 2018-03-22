(function () {
    'use strict';
    var controllerId = 'trade';
    angular.module('avalanchain').controller(controllerId, ['common', '$scope', 'dataservice', 'exchangeservice', '$stateParams','$sce', trade]);

    function trade(common, $scope, dataservice, exchangeservice, $stateParams,$sce) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        vm.info = 'symbol';
        $scope.foo = $stateParams.symbol;
        vm.symbol = $stateParams.symbol;
        //dataservice.getData().then(function (data) {
        //    vm.nodes = data.nodes;
        //    vm.node = vm.nodes.filter(function (node) {
        //        return node.id === nodeId;
        //    })[0];
        //    vm.getTransactions();
        //});
        $scope.tviewurl = $sce.trustAsResourceUrl("https://www.tradingview.com/chart/");
        // $scope.tviewurl = "https://uk.tradingview.com/chart/";
          
        activate();
        function activate() {
            common.activateController([getData()], controllerId)
                .then(function () { log('Activated symbol') });//log('Activated Admin View');
        }


        function getData() {

        }
    };


  


})();