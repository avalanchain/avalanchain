(function () {
    'use strict';
    var controllerId = 'actransactions';
    angular.module('avalanchain').controller(controllerId, ['common', '$scope', 'dataservice', transactions]);

    function transactions(common, $scope, dataservice) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);

        $scope.datayahoo = [];
        $scope.transactions = [];
        $scope.transactionPage = 1;
        $scope.isEdit = false;
        $scope.maxSize = 5;

        setInterval(function updateRandom() {
                getData();
        }, 3000);
        

        var dataprev = [];
        $scope.current = {};
        function getData() {
          dataservice.getData().then(function(data) {
            $scope.data = data;
            $scope.transactions = $scope.data.transactions.filter(function(tran) {
                return tran.account === data.account.address;
            });
          });
        }
        activate();
        function activate() {
            common.activateController([getData()], controllerId)
                .then(function () { log('Activated transactions') });//log('Activated Admin View');
        }
    };


})();
