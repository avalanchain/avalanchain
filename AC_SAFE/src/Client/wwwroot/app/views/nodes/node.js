/// <reference path="create_account.html" />
(function() {
    'use strict';
    var controllerId = 'node';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', '$scope', '$filter', '$uibModal', '$rootScope', '$stateParams', '$interval', node]);

    function node(common, dataservice, $scope, $filter, $uibModal, $rootScope, $stateParams, $interval) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        vm.pieData = [];
        vm.maxSize = 5;
        vm.totalItems = [];
        vm.currentPage = 1;
        vm.transactionPage = 1;

        var nodeId = $stateParams.nodeId;
        dataservice.getData().then(function(data) {
            vm.nodes = data.nodes;
            vm.node = vm.nodes.filter(function(node) {
                return node.id === nodeId;
            })[0];
            vm.getTransactions();
        });
        vm.getTransactions = function() {
            dataservice.getData().then(function(data) {
                vm.transactions = data.transactions.filter(function(transaction) {
                    return transaction.node === nodeId;
                });

                // var currentTransactionPage = vm.transactionPage;
                // $scope.payment.fromAcc = vm.current.ref;
                vm.goupedTypes = {};
                for (var i = 0; i < vm.transactions.length; ++i) {
                    var obj = vm.transactions[i];
                    if (vm.goupedTypes[obj.type] === undefined){
                      vm.goupedTypes[obj.type] = [obj.id];
                    }
                    else{
                      vm.goupedTypes[obj.type].push(obj.id);
                    }

                }
                var t = 0;
                var colors = ["#1ab394", '#79d2c0','#d3d3d3', '#bababa', '#bababa']
                for (var propertyName in vm.goupedTypes) {
                    vm.pieData.push({
                        label: propertyName,
                        data: vm.goupedTypes[propertyName].length,
                        color: colors[t]
                    })
                    t++;
                }

            });
        }

        vm.pieOptions = {
            series: {
                pie: {
                    show: true
                }
            },
            grid: {
                hoverable: true
            },
            tooltip: true,
            tooltipOpts: {
                content: "%p.0%, %s", // show percentages, rounding to 2 decimal places
                shifts: {
                    x: 20,
                    y: 0
                },
                defaultTheme: false
            }
        };

        // vm.pieData = [{
        //     label: "Sales 1",
        //     data: 21,
        //     color: "#d3d3d3"
        // }, {
        //     label: "Sales 2",
        //     data: 3,
        //     color: "#bababa"
        // }, {
        //     label: "Sales 3",
        //     data: 15,
        //     color: "#79d2c0"
        // }, {
        //     label: "Sales 4",
        //     data: 52,
        //     color: "#1ab394"
        // }];

        /**
         * Pie Chart Options
         */

        activate();

        function activate() {
            common.activateController([], controllerId)
                .then(function() {}); //log('Activated Admin View');
        }

    };


})();
