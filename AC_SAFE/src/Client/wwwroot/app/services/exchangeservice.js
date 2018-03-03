
(function () {
    'use strict';

    angular
        .module('avalanchain')
        .factory('exchangeservice', exchangeservice);

    exchangeservice.$inject = ['$http', '$q', 'common', 'dataProvider', '$filter', '$timeout'];
    /* @ngInject */


    function exchangeservice($http, $q, common, dataProvider, $filter, $timeout) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn('exchangeservice');
        var logger = common.logger.getLogFn('exchangeservice');
        var logError = common.logger.getLogFn('exchangeservice', 'error');
        var logWarning = common.logger.getLogFn('exchangeservice', 'warn');
        var data = {};
        var api = {
            submitOrder: 'SubmitOrder',
            symbols: 'Symbols',
            orderStack: 'OrderStack?symbol=',
            mainSymbol: 'MainSymbol',
            getOrders: 'GetOrders',
            orderCommandsCount: 'OrderCommandsCount',
            orderEventsCount: 'OrderEventsCount',
            fullOrdersCount: 'FullOrdersCount',
            symbolOrderCommands: 'SymbolOrderCommands?symbol=',
            symbolOrderEvents: 'SymbolOrderEvents?symbol=',
            symbolOrderCommandsCount: 'SymbolOrderCommandsCount?symbol=',
            symbolOrderEventsCount: 'SymbolOrderEventsCount?symbol=',
            symbolFullOrdersCount: 'SymbolFullOrdersCount?symbol=',
            orderStackView: 'OrderStackView?symbol=',
        };
        var service = {
            submitOrder: submitOrder,
            orderStack: orderStack,
            mainSymbol: mainSymbol,
            getOrders: getOrders,
            orderCommandsCount: orderCommandsCount,
            orderEventsCount: orderEventsCount,
            fullOrdersCount: fullOrdersCount,
            symbols: symbols,
            symbolOrderCommands: symbolOrderCommands,
            symbolOrderEvents: symbolOrderEvents,
            symbolOrderCommandsCount: symbolOrderCommandsCount,
            symbolOrderEventsCount: symbolOrderEventsCount,
            symbolFullOrdersCount: symbolFullOrdersCount,
            orderStackView: orderStackView,
        };

        return service;

        function submitOrder(value) {
            return postData(api.submitOrder, value);
        }

        function orderStack(symbol) {//avalanchain
            return getData(api.orderStack, symbol);
        }

        function mainSymbol() {
            return getData(api.mainSymbol);
        }
        function symbols() {
            return getData(api.symbols);
        }

        function getOrders() {
            return getData(api.getOrders);
        }

        function orderCommandsCount() {
            return getData(api.orderCommandsCount);
        }

        function orderEventsCount() {
            return getData(api.orderEventsCount);
        }

        function fullOrdersCount() {
            return getData(api.fullOrdersCount);
        }

        function symbolOrderCommands(symbol, start, pageSize) {
            return getData(api.symbolOrderCommands, symbol, start, pageSize);
        }

        function symbolOrderEvents(symbol, start, pageSize) {
            return getData(api.symbolOrderEvents, symbol, start, pageSize);
        }

        function symbolOrderCommandsCount(symbol) {
            return getData(api.symbolOrderCommandsCount, symbol);
        }

        function symbolOrderEventsCount(symbol) {
            return getData(api.symbolOrderEventsCount, symbol);
        }

        function symbolFullOrdersCount(symbol) {
            return getData(api.symbolFullOrdersCount, symbol);
        }

        function orderStackView(symbol, depth) {
            return getData(api.orderStackView, symbol, depth);
        }

       

        function getData(link, value, value2, value3) {
            value = value || '';
            value2 = value2 ? '&' + value2: '';
            value3 = value3 ? '&' + value3 : '';
            return dataProvider.get({}, 'api/Exchange/' + link + value + value2 + value3, {},
                function success(data, status) {
                    var st = status;
                    return data;
                },
                function fail(data, status) {
                    //window.location.href = '/';
                });
        }

        function postData(link, value) {
            value = value || '';
            return dataProvider.post({}, 'api/Exchange/' + link , value,
                function success(data, status) {
                    if (status === 200) {
                        logger('Order succefully added!');
                    }
                    var st = status;
                    return data;
                },
                function fail(data, status) {
                    //window.location.href = '/';
                });
        }

        //function getDataaa() {
        //    var defer = $q.defer();

        //    $timeout(function () {
        //        defer.resolve(data);
        //    }, 200);

        //    return defer.promise;
        //}
        //function getId() {
        //    return common.createGuid().replace(/-/gi, '')
        //}
    }
})();
