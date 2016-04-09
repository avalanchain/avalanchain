(function () {
    'use strict';
    var controllerId = 'accounts';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', '$scope','$filter', accounts]);

    function accounts(common, dataservice, $scope, $filter) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        vm.info = 'accounts';
        vm.helloText = 'Welcome in Avalanchain';
        vm.descriptionText = 'CASCADING REACTIVE BLOCKCHAINS';
        $scope.searchAccounts = '';
        $scope.isAccountPage = false;
        $scope.transactions = [];
        $scope.current = {};
        $scope.payment = {
            fromAcc: {},
            toAcc: {}
        };
        $scope.maxSize = 5;
        $scope.totalItems = [];
        $scope.currentPage = 1;
        $scope.transactionPage = 1;
        $scope.transactions = [];

        $scope.getTransactions = function (address) {
            
            $scope.current = $filter('filter')($scope.accounts, {
                ref: { 'address': address }
            }, true)[0];//$scope.current
            return dataservice.getTransactions(address).then(function(data) {
                $scope.transactions = data.data.fields[0].transactions;
                $scope.current.totalTransactions = $scope.transactions.length;
            });
        }
        $scope.showAccount = function (value) {
            var currentTransactionPage = $scope.transactionPage;
            $scope.current = value;
            $scope.transactionPage = $scope.current.ref.address != value.ref.address ? 1 : currentTransactionPage;
            
            $scope.isAccountPage = true;
            $scope.payment.fromAcc = value.ref;
            return $scope.getTransactions($scope.payment.fromAcc.address);
        };

        $scope.newAccount = function() {
            dataservice.newAccount().then(function (data) {
                getAccounts();
            });
        }

        $scope.sendPayment = function () {
            dataservice.sendPayment($scope.payment).then(function (data) {
                $scope.getTransactions($scope.current.ref.address);
                getAccounts();
            });
        }

        $scope.clean = function() {
            $scope.searchAccounts = '';
        }

        $scope.refresh = function () {
            getAccounts();
        }

        setInterval(function updateRandom() {
            
            if ($scope.isAccountPage) {
                $scope.getTransactions($scope.current.ref.address);
            }
                getAccounts();
            
                
        }, 3000);

        function addStatus(data) {
            if (data) {
                for (var i = 0; i < data.length; i++) {
                    if (data[i].status === 'active') {
                        data[i]["navigation"] = 'label-primary';
                    } else {
                        data[i]["navigation"] = 'label-deafault';
                    }
                }
            }
            return data;
        }

        function getAccounts() {
            return dataservice.getAllAccounts().then(function (data) {
                $scope.accounts = addStatus(data.data);
                $scope.totalItems = $scope.accounts.length;
            });

        }

        activate();

        function activate() {
            common.activateController([getAccounts()], controllerId)
                .then(function () { log('Activated Accounts') });//log('Activated Admin View');
        }


        

        
        
    };
   

})();
