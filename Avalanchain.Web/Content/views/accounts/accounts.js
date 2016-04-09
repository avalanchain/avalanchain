﻿(function () {
    'use strict';
    var controllerId = 'accounts';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', '$scope', accounts]);

    function accounts(common, dataservice, $scope) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        vm.info = 'accounts';
        vm.helloText = 'Welcome in Avalanchain';
        vm.descriptionText = 'CASCADING REACTIVE BLOCKCHAINS';
        $scope.searchAccounts = '';
        $scope.valinside = false;
        $scope.transactions = [];
        $scope.payment = {
            fromAcc: {},
            toAcc: {}
        };

        $scope.getTransactions = function (address) {
            $scope.transactions = [];
            return dataservice.getTransactions(address).then(function(data) {
                $scope.transactions = data.data.fields[0].transactions;
                $scope.$digest();
            });
        }
        $scope.showAccount = function (value) {
            $scope.current = value;
            $scope.valinside = true;
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


        activate();

        function activate() {
            common.activateController([getAccounts()], controllerId)
                .then(function () { log('Activated Accounts') });//log('Activated Admin View');
        }


        function getAccounts() {
            return dataservice.getAllAccounts().then(function (data) {
                $scope.accounts = addStatus(data.data);
                $scope.maxSize = 5;
                $scope.totalItems = $scope.accounts.length;
                $scope.currentPage = 1;
            });
            
        }

        
        
    };
   

})();
