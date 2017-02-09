(function () {
    'use strict';
    var controllerId = 'transactions';
    angular.module('avalanchain').controller(controllerId, ['common', '$scope', 'dataservice', transactions]);

    function transactions(common, $scope, dataservice) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);

        var vm = this;

        $scope.datayahoo = [];
        $scope.transactions = [];
        $scope.transactionPage = 1;
        $scope.isEdit = false;
        $scope.maxSize = 5;

        dataservice.getTransactions(vm);
        // setInterval(function updateRandom() {
        //         getData();
        // }, 3000);

        $scope.$on("$destroy", function() {
            if (angular.isDefined(vm.removeListener)) {
                vm.removeListener(vm);
            }
        });
        var dataprev = [];
        $scope.current = {};
        // function getData() {
        //   dataservice.getData().then(function(data) {
        //     $scope.data = data;
        //     $scope.transactions = $scope.data.transactions;
        //   });
        // }
        activate();
        function activate() {
            common.activateController([], controllerId)
                .then(function () { log('Activated transactions') });//log('Activated Admin View');
        }
    };


})();
