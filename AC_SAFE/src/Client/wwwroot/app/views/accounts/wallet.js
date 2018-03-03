/// <reference path="create_account.html" />
(function() {
    'use strict';
    var controllerId = 'wallet';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', '$scope', '$filter', '$uibModal', '$rootScope', '$stateParams', '$interval', '$state', wallet]);

    function wallet(common, dataservice, $scope, $filter, $uibModal, $rootScope, $stateParams, $interval, $state) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;

        var accountId = $stateParams.accountId;

        $scope.currencies = [{
            id: 0,
            currency: 'AVC',
            name: 'AVCOIN'
        },{
            id: 1,
            currency: 'USD',
            name: 'Dollar'
        },{
            id: 2,
            currency: 'EUR',
            name: 'Euro'
        },{
            id: 3,
            currency: 'GBP',
            name: 'British Pound'
        }];
        $scope.currency = $scope.currencies[0];
        dataservice.getData().then(function(data) {
            $scope.accounts = data.accounts;
            $scope.current = data.accounts.filter(function(acc) {
                return acc.ref.address === accountId;
            })[0];

            if(!$scope.current){
              $state.go('index.accounts');
            }
            // $scope.getTransactions();
        });


        $scope.openSend = function (currency) {
            $rootScope.modal = {};
            $rootScope.modal.guid = dataservice.getId();
            $rootScope.modal.from = $scope.current;
            $rootScope.modal.to = {};
            $rootScope.modal.currency = currency;
            $rootScope.modal.accounts = $scope.accounts;
            $rootScope.modal.ok =function () {
              log('Money have sent succesfully to: '+ $rootScope.modal.to.name);
                // dataservice.newAccount().then(function (data) {
                //     $rootScope.$emit('updateAccounts');
                // });
                $uibModalInstance.close();
            };
            var modalInstance = $uibModal.open({
                templateUrl: '/app/views/accounts/send.html',
                controller: modalCtrl
            });
        };

        $scope.openExchange = function (currency) {
          $rootScope.modal = {};
          $rootScope.modal.guid = dataservice.getId();
          $rootScope.modal.from = $scope.current;
          $rootScope.modal.to = {};
          $rootScope.modal.currency = currency;
          $rootScope.modal.currencies = $scope.currencies;
          $rootScope.modal.accounts = $scope.accounts;
            $rootScope.modal.ok =function () {
              log('Exchenged succefully!');
                // dataservice.newAccount().then(function (data) {
                //     $rootScope.$emit('updateAccounts');
                // });
                $uibModalInstance.close();
            };
            var modalInstance = $uibModal.open({
                templateUrl: '/app/views/accounts/exchange.html',
                controller: modalCtrl
            });
        };

        $scope.transactions = [];

        $scope.payment = {
            fromAcc: {},
            toAcc: {}
        };
        //TODO: pagination add to service
        $scope.maxSize = 5;
        $scope.totalItems = [];
        $scope.currentPage = 1;
        $scope.transactionPage = 1;

        // $scope.Timer = setInterval(function updateRandom() {
        //     $scope.getTransactions();
        // }, 3000);


        // $scope.getTransactions = function() {
        //     dataservice.getData().then(function(data) {
        //         $scope.transactions = data.transactions.filter(function(transaction) {
        //             return transaction.account === accountId;
        //         });
        //
        //         var currentTransactionPage = $scope.transactionPage;
        //         $scope.payment.fromAcc = $scope.current.ref;
        //     });
        // }

        $scope.sendPayment = function() {
            dataservice.sendPayment($scope.payment).then(function(data) {
                $scope.getTransactions($scope.current.ref.address);
                getAccounts();
            });
        }

        // return data;
        // }



        // $scope.startTimer = function() {
        //     $scope.Timer = $interval($scope.getTransactions, 3000);
        // };
        //
        // //TODO: add to service
        // $scope.$on("$destroy", function() {
        //     if (angular.isDefined($scope.Timer)) {
        //         $interval.cancel($scope.Timer);
        //     }
        // });
        // $scope.startTimer();

        activate();

        function activate() {
            common.activateController([], controllerId)
                .then(function() {}); //log('Activated Admin View');
        }

    };


})();
