/// <reference path="create_account.html" />
(function () {
    'use strict';
    var controllerId = 'accounts';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', '$scope', '$filter', '$uibModal', '$rootScope', '$state', accounts]);

    function accounts(common, dataservice, $scope, $filter, $uibModal, $rootScope, $state) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        $scope.searchAccounts = '';
        // $scope.isAccountPage = false;
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


        $scope.openModal = function () {
            var m = new Mnemonic(96);
            $rootScope.modal = {};
            $rootScope.modal.password = m.toWords().join(' ');;
            $rootScope.modal.hexPass = m.toHex();
            $rootScope.modal.guid = dataservice.getId();
            $rootScope.modal.ok =function () {
                dataservice.newAccount().then(function (data) {
                    $rootScope.$emit('updateAccounts');
                });
            };
            var modalInstance = $uibModal.open({
                templateUrl: '/app/views/accounts/create_account.html',
                controller: modalCtrl
            });
        };
        //$scope.showModal = false;
        //$scope.toggleModal = function () {
        //    $scope.showModal = !$scope.showModal;
        //};
        $rootScope.$on('updateAccounts', function () {
            getAccounts();
        });

        $scope.getTransactions = function (address) {

            $scope.current = $filter('filter')($scope.accounts, {
                ref: { 'address': address }
            }, true)[0];//$scope.current
            // return dataservice.getTransactions(address).then(function(data) {
            //     $scope.transactions = data.data.fields[0].transactions;
            //     $scope.current.totalTransactions = $scope.transactions.length;
            // });
        }

        $scope.showAccount = function(account) {
            $state.go('index.account', {
                accountId: account.ref.address
            });
        }

        $scope.showWallet = function(account) {
            $state.go('index.wallet', {
                accountId: account.ref.address
            });
        }
        // $scope.showAccount = function (value) {
        //     var currentTransactionPage = $scope.transactionPage;
        //     $scope.current = value;
        //     $scope.transactionPage = $scope.current.ref.address != value.ref.address ? 1 : currentTransactionPage;
        //
        //     $scope.isAccountPage = true;
        //     $scope.payment.fromAcc = value.ref;
        //     //return $scope.getTransactions($scope.payment.fromAcc.address);
        // };

        //$scope.newAccount = function() {
        //    dataservice.newAccount().then(function (data) {
        //        getAccounts();
        //    });
        //}

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
                    if (data[i].status === 1) {
                        data[i]["navigation"] = 'label-primary';
                    } else {
                        data[i]["navigation"] = 'label-deafault';
                    }
                }
            }
            return data;
        }

        function getAccounts() {
          dataservice.getData().then(function(data) {
            $scope.accounts = addStatus(data.accounts);
            $scope.totalItems = $scope.accounts.length;
            // $rootScope.accountsamount = $scope.accounts.length;
          });
            // $scope.accounts = addStatus(dataservice.getData().accounts);
            // $scope.totalItems = $scope.accounts.length;
            // $rootScope.accountsamount = $scope.accounts.length;
        }

        activate();

        function activate() {
            common.activateController([getAccounts()], controllerId)
                .then(function () { log('Activated Accounts') });//log('Activated Admin View');
        }






    };


})();
