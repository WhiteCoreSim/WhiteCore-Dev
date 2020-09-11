/**
 * Created by PhpStorm.
 * @author: MackRais
 * @site: http://mackrais.com
 * @link
  * @email: mackrais@gmail.com
 */

/**
 * Create HTML template
 */
function createTemplateBBAlert () {
  var $block = $('<div class="bb-alert alert alert-deng" data-role="mr-bb-alert">'
    + '<a href="javascript:void(0)" class="mr-close-alert" onclick="$(this).parent().hide()">✘</a>'
    + '<span></span>'
    + '</div>');
  $('body').prepend($block)
  return $block
}

/**
 * Sorting items
 * @param indentElement
 */
function sortAlertItems (indentElement) {
  var topHeight = $('[data-role="mr-bb-alert"]').eq(0).height() + indentElement
  $('[data-role="mr-bb-alert"]').each(function (i, item) {
    var $next = $(item).next('[data-role="mr-bb-alert"]').eq(0)
    if ($next.length) {
      $next.css('margin-bottom', topHeight+ 'px')
      topHeight = topHeight + $(item).height() + indentElement
    }
  })
}

/**
 * Show one of the alert type
 *
 * @param text
 * @param status
 * @param endShowTime
 * @param startShowTime
 * @constructor
 */
function MsgAlert (text, status, endShowTime, startShowTime) {
  var $block = createTemplateBBAlert()
  text = typeof text !== 'undefined' ? text : 'Empty!'
  status = typeof status !== 'undefined' ? status : 'info'
  startShowTime = typeof (startShowTime) !== 'undefined' ? startShowTime : 0
  endShowTime = typeof (endShowTime) !== 'undefined' ? endShowTime : 3000
  $block.removeClass().find('span').empty().append(text)
  $block.addClass('bb-alert alert alert-' + status)
  $block.delay(startShowTime).show(500)
  $block.delay(endShowTime).hide(500)
  setTimeout(function () {
    sortAlertItems(25)
  },501)
  setTimeout(function () {
    $block.remove()
  }, endShowTime)
}

/**
 * Show success alert
 * @param text
 * @param endShowTime
 * @param startShowTime
 * @constructor
 */
function MsgSuccess (text, endShowTime, startShowTime) {
  MsgAlert(text, 'success', endShowTime, startShowTime)
}

/**
 * Show error(danger) alert
 * @param text
 * @param endShowTime
 * @param startShowTime
 * @constructor
 */
function MsgError (text, endShowTime, startShowTime) {
  MsgAlert(text, 'danger', endShowTime, startShowTime)
}

/**
 * Show warning alert
 * @param text
 * @param endShowTime
 * @param startShowTime
 * @constructor
 */
function MsgWarning (text, endShowTime, startShowTime) {
  MsgAlert(text, 'warning', endShowTime, startShowTime)
}

/**
 * Show info alert
 * @param text
 * @param endShowTime
 * @param startShowTime
 * @constructor
 */
function MsgInfo (text, endShowTime, startShowTime) {
  MsgAlert(text, 'info', endShowTime, startShowTime)
}